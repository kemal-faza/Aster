#!/usr/bin/env bash
# Validate the WPF reference capture set against scripts/wpf-capture-manifest.json.
#
# This proves completeness and file integrity only. It does not prove visual
# parity; parity needs the side-by-side review described in
# docs/plans/2026-11-18-wpf-capture-runbook.md.
#
# Usage:
#   ./scripts/check-wpf-captures.sh [dir] [--set p0|p1|all] [--group p0|4b|4c|4d|4e|4f]
#                                   [--manifest path] [--strict-optional]
#
# Exit codes: 0 complete, 1 incomplete/failed, 2 environment or usage error.

set -uo pipefail

# Locale-independent numeric parsing (the repo host may run id_ID).
export LC_ALL=C

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$script_dir/.." && pwd)"

capture_dir="$root/docs/results/phase4-reference/wpf"
manifest="$script_dir/wpf-capture-manifest.json"
filter_set="all"
filter_group=""
strict_optional=0

usage() {
    sed -n '2,16p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [ $# -gt 0 ]; do
    case "$1" in
        --set)
            filter_set="${2:-}"
            shift 2
            ;;
        --group)
            filter_group="${2:-}"
            shift 2
            ;;
        --manifest)
            manifest="${2:-}"
            shift 2
            ;;
        --strict-optional)
            strict_optional=1
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        -*)
            printf 'unknown option: %s\n' "$1" >&2
            exit 2
            ;;
        *)
            capture_dir="$1"
            shift
            ;;
    esac
done

case "$filter_set" in
    p0|p1|all) ;;
    *) printf 'invalid --set: %s\n' "$filter_set" >&2; exit 2 ;;
esac

if [ ! -f "$manifest" ]; then
    printf 'manifest not found: %s\n' "$manifest" >&2
    exit 2
fi

if ! command -v jq >/dev/null 2>&1; then
    printf 'jq is required to read the manifest\n' >&2
    exit 2
fi

if ! jq -e '.artifacts and (.artifacts | length > 0)' "$manifest" >/dev/null 2>&1; then
    printf 'manifest is not valid JSON or has no artifacts: %s\n' "$manifest" >&2
    exit 2
fi

preset_filter=$(jq -r '.settingsKeys | map("has(\"" + . + "\")") | join(" and ")' "$manifest")
preset_keys=$(jq -c '.settingsKeys' "$manifest")

printf 'capture set: %s\n' "$capture_dir"
printf 'manifest:    %s\n' "$manifest"
printf 'filter:      set=%s group=%s\n\n' "$filter_set" "${filter_group:-all}"

if [ ! -d "$capture_dir" ]; then
    printf 'capture directory does not exist.\n' >&2
    printf 'Capture on Windows first; see docs/plans/2026-11-18-wpf-capture-runbook.md\n' >&2
    exit 1
fi

info="$capture_dir/info.md"

# --- helpers ---------------------------------------------------------------

png_magic() {
    local f="$1"
    [ "$(od -An -tx1 -N8 "$f" 2>/dev/null | tr -d ' \n')" = "89504e470d0a1a0a" ]
}

jpeg_magic() {
    local f="$1"
    [ "$(od -An -tx1 -N3 "$f" 2>/dev/null | tr -d ' \n')" = "ffd8ff" ]
}

# Prints "width height" for a PNG, using the IHDR chunk.
png_dims() {
    local f="$1" b
    b=$(od -An -tu1 -j16 -N8 "$f" 2>/dev/null | tr -s ' ' '\n' | sed '/^$/d')
    # shellcheck disable=SC2086
    set -- $b
    if [ $# -ne 8 ]; then
        return 1
    fi
    printf '%s %s\n' \
        "$(( $1 * 16777216 + $2 * 65536 + $3 * 256 + $4 ))" \
        "$(( $5 * 16777216 + $6 * 65536 + $7 * 256 + $8 ))"
}

declare -A size_cache=()
expected_size() {
    local capture="$1" scale="$2" key pair
    key="$capture-$scale"
    if [ -n "${size_cache[$key]:-}" ]; then
        printf '%s\n' "${size_cache[$key]}"
        return 0
    fi
    case "$capture" in
        canvas)
            pair=$(jq -r --arg s "$scale" '.canvasSizes[$s] | "\(.[0]) \(.[1])"' "$manifest")
            ;;
        settings)
            pair=$(jq -r --arg s "$scale" '.settingsSizes[$s] | "\(.[0]) \(.[1])"' "$manifest")
            ;;
        min)
            pair=$(jq -r '"\(.minSize.width) \(.minSize.height)"' "$manifest")
            ;;
        *)
            return 1
            ;;
    esac
    if [ -z "$pair" ] || [ "$pair" = "null null" ]; then
        return 1
    fi
    size_cache[$key]="$pair"
    printf '%s\n' "$pair"
}

scale_from_path() {
    local p="$1"
    if [[ "$p" =~ screenshots/([0-9]+)- ]]; then
        printf '%s\n' "${BASH_REMATCH[1]}"
    else
        printf '\n'
    fi
}

short() {
    # Compact size formatter used in the report.
    printf '%sx%s' "$1" "$2"
}

# --- counters --------------------------------------------------------------

total_ok=0
total_missing=0
total_declared=0
total_failed=0
total_skipped=0
total_optional_missing=0

declare -A g_req=() g_ok=() g_missing=() g_declared=() g_failed=() g_skipped=()

add_group() {
    local g="$1"
    g_req[$g]=$(( ${g_req[$g]:-0} ))
    g_ok[$g]=$(( ${g_ok[$g]:-0} ))
    g_missing[$g]=$(( ${g_missing[$g]:-0} ))
    g_declared[$g]=$(( ${g_declared[$g]:-0} ))
    g_failed[$g]=$(( ${g_failed[$g]:-0} ))
    g_skipped[$g]=$(( ${g_skipped[$g]:-0} ))
}

report_line() {
    # status id path detail
    printf '%-6s %-8s %-62s %s\n' "$1" "$2" "$3" "${4:-}"
}

# --- artifact loop ---------------------------------------------------------

shopt -s nullglob

while IFS=$'\x1f' read -r id set group kind path capture required condition note duration count; do
    [ -n "$id" ] || continue
    if [ "$filter_set" != "all" ] && [ "$set" != "$filter_set" ]; then
        continue
    fi
    if [ -n "$filter_group" ] && [ "$group" != "$filter_group" ]; then
        continue
    fi

    add_group "$group"
    if [ "$required" = "true" ]; then
        g_req[$group]=$(( ${g_req[$group]} + 1 ))
    fi

    full="$capture_dir/$path"

    case "$kind" in
        sample)
            files=( "$capture_dir"/$path )
            found=${#files[@]}
            want="${count:-1}"
            if [ "$found" -lt "$want" ]; then
                if [ "$required" = "true" ]; then
                    report_line "MISS" "$id" "$path" "found $found of $want JPEGs"
                    total_missing=$((total_missing + 1))
                    g_missing[$group]=$(( ${g_missing[$group]} + 1 ))
                else
                    report_line "OPT" "$id" "$path" "found $found of $want"
                    total_optional_missing=$((total_optional_missing + 1))
                    if [ "$strict_optional" -eq 1 ]; then
                        total_failed=$((total_failed + 1))
                        g_failed[$group]=$(( ${g_failed[$group]} + 1 ))
                    fi
                fi
                continue
            fi
            bad=0
            for f in "${files[@]}"; do
                if ! jpeg_magic "$f"; then
                    report_line "FAIL" "$id" "${f#"$capture_dir"/}" "not a JPEG"
                    bad=1
                    break
                fi
                if [ "$(stat -c %s "$f" 2>/dev/null || echo 0)" -lt 20000 ]; then
                    report_line "FAIL" "$id" "${f#"$capture_dir"/}" "smaller than 20 KB"
                    bad=1
                    break
                fi
            done
            if [ "$bad" -eq 0 ]; then
                report_line "OK" "$id" "$path" "$found JPEGs"
                total_ok=$((total_ok + 1))
                g_ok[$group]=$(( ${g_ok[$group]} + 1 ))
            else
                total_failed=$((total_failed + 1))
                g_failed[$group]=$(( ${g_failed[$group]} + 1 ))
            fi
            ;;

        *)
            if [ ! -f "$full" ]; then
                if [ "$required" = "true" ]; then
                    report_line "MISS" "$id" "$path" "${condition:-required}"
                    total_missing=$((total_missing + 1))
                    g_missing[$group]=$(( ${g_missing[$group]} + 1 ))
                elif [ -f "$info" ] && grep -qF -- "$id" "$info"; then
                    report_line "DECL" "$id" "$path" "${condition:-optional}"
                    total_declared=$((total_declared + 1))
                    g_declared[$group]=$(( ${g_declared[$group]} + 1 ))
                else
                    report_line "OPT" "$id" "$path" "${condition:-optional}"
                    total_optional_missing=$((total_optional_missing + 1))
                    if [ "$strict_optional" -eq 1 ]; then
                        total_failed=$((total_failed + 1))
                        g_failed[$group]=$(( ${g_failed[$group]} + 1 ))
                    fi
                fi
                continue
            fi

            case "$kind" in
                screenshot)
                    if ! png_magic "$full"; then
                        report_line "FAIL" "$id" "$path" "not a PNG"
                        total_failed=$((total_failed + 1))
                        g_failed[$group]=$(( ${g_failed[$group]} + 1 ))
                        continue
                    fi
                    dims=$(png_dims "$full") || {
                        report_line "FAIL" "$id" "$path" "unreadable PNG header"
                        total_failed=$((total_failed + 1))
                        g_failed[$group]=$(( ${g_failed[$group]} + 1 ))
                        continue
                    }
                    read -r w h <<< "$dims"
                    scale=$(scale_from_path "$path")
                    if [ -z "$scale" ]; then
                        report_line "FAIL" "$id" "$path" "path has no scale folder"
                        total_failed=$((total_failed + 1))
                        g_failed[$group]=$(( ${g_failed[$group]} + 1 ))
                        continue
                    fi
                    if ! want=$(expected_size "$capture" "$scale"); then
                        report_line "FAIL" "$id" "$path" "unknown capture mode $capture"
                        total_failed=$((total_failed + 1))
                        g_failed[$group]=$(( ${g_failed[$group]} + 1 ))
                        continue
                    fi
                    read -r ew eh <<< "$want"
                    if [ "$capture" = "min" ]; then
                        if [ "$w" -lt "$ew" ] || [ "$h" -lt "$eh" ]; then
                            report_line "FAIL" "$id" "$path" \
                                "$(short "$w" "$h") smaller than $(short "$ew" "$eh")"
                            total_failed=$((total_failed + 1))
                            g_failed[$group]=$(( ${g_failed[$group]} + 1 ))
                            continue
                        fi
                    elif [ "$w" -ne "$ew" ] || [ "$h" -ne "$eh" ]; then
                        report_line "FAIL" "$id" "$path" \
                            "$(short "$w" "$h"), expected $(short "$ew" "$eh")"
                        total_failed=$((total_failed + 1))
                        g_failed[$group]=$(( ${g_failed[$group]} + 1 ))
                        continue
                    fi
                    if [ "$(stat -c %s "$full" 2>/dev/null || echo 0)" -lt 256 ]; then
                        report_line "FAIL" "$id" "$path" "suspiciously small PNG"
                        total_failed=$((total_failed + 1))
                        g_failed[$group]=$(( ${g_failed[$group]} + 1 ))
                        continue
                    fi
                    report_line "OK" "$id" "$path" "$(short "$w" "$h")"
                    total_ok=$((total_ok + 1))
                    g_ok[$group]=$(( ${g_ok[$group]} + 1 ))
                    ;;

                video)
                    if ! command -v ffprobe >/dev/null 2>&1; then
                        if [ "$required" = "true" ]; then
                            report_line "FAIL" "$id" "$path" "ffprobe unavailable; required video is unverified"
                            total_failed=$((total_failed + 1))
                            g_failed[$group]=$(( ${g_failed[$group]} + 1 ))
                        else
                            report_line "SKIP" "$id" "$path" "ffprobe unavailable"
                            total_skipped=$((total_skipped + 1))
                            g_skipped[$group]=$(( ${g_skipped[$group]} + 1 ))
                        fi
                        continue
                    fi
                    dur=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$full" 2>/dev/null | head -n 1)
                    if [ -z "$dur" ]; then
                        report_line "FAIL" "$id" "$path" "ffprobe could not read it"
                        total_failed=$((total_failed + 1))
                        g_failed[$group]=$(( ${g_failed[$group]} + 1 ))
                        continue
                    fi
                    min="${duration%-*}"
                    max="${duration#*-}"
                    if ! awk -v d="$dur" -v lo="$min" -v hi="$max" \
                        'BEGIN { exit !(d + 0 >= lo - 0.5 && d + 0 <= hi + 0.5) }'; then
                        report_line "FAIL" "$id" "$path" \
                            "$(printf '%.1fs' "$dur"), expected ${min}-${max}s"
                        total_failed=$((total_failed + 1))
                        g_failed[$group]=$(( ${g_failed[$group]} + 1 ))
                        continue
                    fi
                    report_line "OK" "$id" "$path" "$(printf '%.1fs' "$dur")"
                    total_ok=$((total_ok + 1))
                    g_ok[$group]=$(( ${g_ok[$group]} + 1 ))
                    ;;

                preset)
                    if ! jq -e . "$full" >/dev/null 2>&1; then
                        report_line "FAIL" "$id" "$path" "invalid JSON"
                        total_failed=$((total_failed + 1))
                        g_failed[$group]=$(( ${g_failed[$group]} + 1 ))
                        continue
                    fi
                    if ! jq -e "$preset_filter" "$full" >/dev/null 2>&1; then
                        missing=$(jq -r --argjson want "$preset_keys" \
                            '($want - (keys)) | join(", ")' "$full")
                        report_line "FAIL" "$id" "$path" "missing keys: ${missing:-?}"
                        total_failed=$((total_failed + 1))
                        g_failed[$group]=$(( ${g_failed[$group]} + 1 ))
                        continue
                    fi
                    report_line "OK" "$id" "$path" "22 keys"
                    total_ok=$((total_ok + 1))
                    g_ok[$group]=$(( ${g_ok[$group]} + 1 ))
                    ;;

                *)
                    report_line "FAIL" "$id" "$path" "unknown kind $kind"
                    total_failed=$((total_failed + 1))
                    g_failed[$group]=$(( ${g_failed[$group]} + 1 ))
                    ;;
            esac
            ;;
    esac
done < <(jq -r '
    .artifacts[]
    | [ .id, .set, .group, .kind, .path, (.capture // ""),
        (.required | tostring), (.condition // ""), (.note // ""),
        ((.duration // []) | join("-")), ((.count // 0) | tostring) ]
    | join("\u001f")' "$manifest")

if [ "${#g_req[@]}" -eq 0 ]; then
    printf 'no artifacts matched set=%s group=%s\n' "$filter_set" "${filter_group:-all}" >&2
    exit 1
fi

# --- info.md ---------------------------------------------------------------

printf '\ninfo.md\n'
info_failed=0
if [ ! -f "$info" ]; then
    printf 'FAIL   info.md missing\n'
    info_failed=1
else
    while IFS= read -r field; do
        if awk -v f="$field" '
            index($0, "- " f ":") == 1 {
                v = substr($0, length("- " f ":") + 1)
                gsub(/^[ \t]+|[ \t]+$/, "", v)
                if (v != "") found = 1
            }
            END { exit found ? 0 : 1 }' "$info"; then
            printf 'OK     %s\n' "$field"
        else
            printf 'FAIL   %s is empty\n' "$field"
            info_failed=$((info_failed + 1))
        fi
    done < <(jq -r '.requiredInfoFields[]' "$manifest")
fi

# --- summary ---------------------------------------------------------------

printf '\n%-6s %9s %9s %9s %9s %9s\n' GROUP REQUIRED OK MISSING DECLARED FAILED
for g in $(printf '%s\n' "${!g_req[@]}" | sort); do
    printf '%-6s %9s %9s %9s %9s %9s\n' \
        "$g" "${g_req[$g]}" "${g_ok[$g]}" "${g_missing[$g]}" \
        "${g_declared[$g]}" "${g_failed[$g]}"
done

printf '\nartifacts: %s ok, %s missing, %s declared, %s failed, %s skipped\n' \
    "$total_ok" "$total_missing" "$total_declared" "$total_failed" "$total_skipped"
if [ "$total_optional_missing" -gt 0 ]; then
    printf 'optional:  %s missing and not declared' "$total_optional_missing"
    if [ "$strict_optional" -eq 1 ]; then
        printf ' (counted as failure)\n'
    else
        printf ' (pass --strict-optional to fail on these)\n'
    fi
fi
if [ "$info_failed" -gt 0 ]; then
    if [ ! -f "$info" ]; then
        printf 'info.md:   missing\n'
    else
        printf 'info.md:   %s field(s) empty\n' "$info_failed"
    fi
fi

printf '\nNote: a complete set is reviewable, not proven equal. Record per-state\n'
printf 'PASS/PARTIAL after the side-by-side review in docs/results/phase4a-report.md.\n'

if [ "$total_missing" -gt 0 ] || [ "$total_failed" -gt 0 ] || [ "$info_failed" -gt 0 ]; then
    printf '\nRESULT: INCOMPLETE\n'
    exit 1
fi

if [ "$total_optional_missing" -gt 0 ] && [ "$strict_optional" -eq 1 ]; then
    printf '\nRESULT: INCOMPLETE\n'
    exit 1
fi

printf '\nRESULT: COMPLETE\n'
exit 0
