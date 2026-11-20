#!/usr/bin/env bash
# NativeAOT smoke for the Phase 4A shell.
#
# Publishes the Linux head, launches it with a throwaway HOME that contains a
# known schema-3 settings file, and verifies the file survives the startup
# reconciliation unchanged, the effect layer is mapped (A1), and the publish log
# contains no trim/AOT warnings.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

out="artifacts/linux-x64"
log="artifacts/linux-aot.log"
mkdir -p artifacts
rm -rf "$out"

publish_rc=0
dotnet publish src/Aster.Avalonia/Aster.Desktop.Linux/Aster.Desktop.Linux.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=partial \
  -p:StripSymbols=true -p:InvariantGlobalization=false -p:DebugType=None \
  -p:CopyOutputSymbolsToPublishDirectory=false -o "$out" 2>&1 | tee "$log" ||
  publish_rc=$?
if [ "$publish_rc" -ne 0 ]; then
    echo "FAIL: AOT publish failed (exit $publish_rc)" >&2
    exit 1
fi

if grep -Eq 'IL2[0-9]{3}|IL3[0-9]{3}' "$log"; then
    echo "FAIL: trim/AOT warning in the publish log" >&2
    grep -E 'IL2[0-9]{3}|IL3[0-9]{3}' "$log" >&2 || true
    exit 1
fi

exe="$out/Aster.Avalonia"
[ -x "$exe" ] || { echo "FAIL: AOT executable missing" >&2; exit 1; }

work="$(mktemp -d "${TMPDIR:-/tmp}/aster-aot.XXXXXX")"
settings="$work/home/.local/share/AsterLab/Avalonia/settings.json"
ready="$work/ready"
app_log="$work/app.log"
pid=""
effect_status="PASS"

cleanup() {
    if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
        kill "$pid" 2>/dev/null || true
        for _ in $(seq 1 60); do
            kill -0 "$pid" 2>/dev/null || break
            sleep 0.05
        done
        kill -9 "$pid" 2>/dev/null || true
        wait "$pid" 2>/dev/null || true
    fi
    rm -rf "$work"
}
trap cleanup EXIT

mkdir -p "$(dirname "$settings")" "$work/config"
cat >"$settings" <<'JSON'
{"schemaVersion":3,"startWithWindows":false,"minimalNotch":false,"hideInFullscreen":true,
 "glassBackground":true,"bouncySpring":true,"showMascot":true,"volumeToast":true,
 "brightnessToast":true,"lyricsEnabled":true,"lyricsHighlightColor":"#7C9CFF",
 "waterReminderEnabled":false,"waterIntervalMinutes":60,"waterActiveStartHour":8,
 "waterActiveEndHour":22,"waterDailyGoal":8,"waterGlassesToday":0,"waterDate":"",
 "cameraMirror":true,"focusMinutes":25,"shortBreakMinutes":5,"longBreakMinutes":15,
 "focusChime":true,"audioReactiveBars":true}
JSON
seeded="$(cat "$settings")"

HOME="$work/home" \
XDG_DATA_HOME="$work/home/.local/share" \
XDG_CONFIG_HOME="$work/config" \
ASTER_READY_FILE="$ready" \
    "$exe" >"$app_log" 2>&1 &
pid=$!

for _ in $(seq 1 200); do
    [ -f "$ready" ] && break
    kill -0 "$pid" 2>/dev/null || {
        echo "FAIL: AOT app exited before the ready file" >&2
        tail -n 40 "$app_log" >&2 || true
        exit 1
    }
    sleep 0.05
done
[ -f "$ready" ] || {
    echo "FAIL: AOT ready file not written" >&2
    tail -n 40 "$app_log" >&2 || true
    exit 1
}
echo "AOT ready in $(cat "$ready") ms"
sleep 0.3

# The file must still be schema-3 with the seeded values.
python3 - "$settings" "$seeded" <<'PY' || {
import json
import sys

path, seeded_text = sys.argv[1], sys.argv[2]
seeded = json.loads(seeded_text)
with open(path, encoding="utf-8") as handle:
    loaded = json.load(handle)

if loaded.get("schemaVersion") != 3:
    raise SystemExit(f"schemaVersion is {loaded.get('schemaVersion')!r}, expected 3")

missing = []
changed = []
for key, expected in seeded.items():
    if key not in loaded:
        missing.append(key)
    elif loaded[key] != expected:
        changed.append((key, expected, loaded[key]))

if missing or changed:
    raise SystemExit(f"settings changed or missing: missing={missing} changed={changed}")

print(f"settings file intact: schemaVersion=3, {len(seeded)} seeded keys preserved")
PY
    echo "FAIL: schema-3 settings file did not survive startup (see above)" >&2
    exit 1
}

# Effect layer: A1 PASS when the X11 window is created and mapped for the app.
# It stays mapped while the island is collapsed so the collapsed panel keeps a
# neutral shadow; the accent glow itself is opacity 0 collapsed.
effect_wid=""
effect_visible=""
for _ in $(seq 1 80); do
    for candidate in $(xdotool search --name '^Aster Effect Layer$' 2>/dev/null || true); do
        if [ "$(xprop -id "$candidate" _NET_WM_PID 2>/dev/null | sed -n 's/.*= //p')" = "$pid" ]; then
            effect_wid="$candidate"
        fi
    done
    [ -n "$effect_wid" ] && break
    sleep 0.05
done

if [ -n "$effect_wid" ]; then
    effect_visible=""
    # The effect window is mapped for the collapsed shadow; wait for it to
    # appear instead of relying on the creation order at startup.
    for _ in $(seq 1 40); do
        effect_visible="$(xdotool search --onlyvisible --name '^Aster Effect Layer$' 2>/dev/null | while read -r candidate; do
            [ "$(xprop -id "$candidate" _NET_WM_PID 2>/dev/null | sed -n 's/.*= //p')" = "$pid" ] && echo "$candidate"
        done | tail -1 || true)"
        [ -n "$effect_visible" ] && break
        sleep 0.05
    done
    if [ -n "$effect_visible" ]; then
        echo "A1 PASS: Aster Effect Layer is mapped for the collapsed shadow (window $effect_visible)"
    else
        echo "A1 PARTIAL: effect window was created but did not map for the collapsed shadow (window $effect_wid)"
        effect_status="PARTIAL"
    fi
else
    echo "PARTIAL: A1 effect window 'Aster Effect Layer' was not created for pid $pid; effect layer may have self-disabled"
    echo "reason: xdotool found no window matching '^Aster Effect Layer$' owned by the app within 4s"
    tail -n 10 "$app_log" >&2 || true
    effect_status="PARTIAL"
fi

bytes="$(stat -c '%s' "$exe")"
echo "AOT binary: $(du -h "$exe" | cut -f1) ($bytes bytes)"
echo "AOT log: $log"

if [ "$effect_status" = "PASS" ]; then
    echo "Linux AOT probe PASS"
    exit 0
fi

echo "Linux AOT probe PARTIAL (effect layer fallback)"
exit 3
