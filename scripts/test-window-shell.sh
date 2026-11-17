#!/usr/bin/env bash
set -euo pipefail

exe="${1:-src/Aster.Avalonia/Aster.Desktop.Linux/bin/Release/net10.0/Aster.Avalonia}"
scale="${EXPECTED_SCALE:-2}"
work="$(mktemp -d)"
ready=""
log=""
pid=""
under_pid=""
case_dir=""
focus_check="PASS"
pointer_warp="no"
away_x=10
away_y=10

mkdir -p docs/results/screenshots/phase2

fail() {
    echo "FAIL: $*"
    exit 1
}

note() {
    echo "note: $*"
}

# KWin ignores XWayland pointer warps (XTest motion), so on this host the probe
# cannot move the real pointer. It still exercises the same XQueryPointer hover
# path by moving the island window away from the real pointer, then under it.
probe_pointer_warp() {
    local before after
    before="$(xdotool getmouselocation --shell | tr '\n' ' ')"
    xdotool mousemove 10 10 2>/dev/null || true
    sleep 0.1
    after="$(xdotool getmouselocation --shell | tr '\n' ' ')"
    if [ "$before" != "$after" ]; then
        pointer_warp="yes"
    fi
}

pointer_position() {
    eval "$(xdotool getmouselocation --shell | sed 's/^/PTR_/')"
}

stop_app() {
    if [ -n "${pid:-}" ] && kill -0 "$pid" 2>/dev/null; then
        kill "$pid" 2>/dev/null || true
        for _ in $(seq 1 60); do
            kill -0 "$pid" 2>/dev/null || break
            sleep 0.05
        done
        kill -9 "$pid" 2>/dev/null || true
        wait "$pid" 2>/dev/null || true
    fi
    pid=""
}

cleanup() {
    if [ -n "${under_pid:-}" ]; then
        kill "$under_pid" 2>/dev/null || true
        wait "$under_pid" 2>/dev/null || true
    fi
    stop_app
    rm -rf "$work"
}
trap cleanup EXIT

write_settings() {
    local minimal="$1"
    mkdir -p "$case_dir/home" "$case_dir/data/AsterLab/Avalonia" "$case_dir/config"
    cat >"$case_dir/data/AsterLab/Avalonia/settings.json" <<EOF
{
  "schemaVersion": 2,
  "startWithWindows": false,
  "minimalNotch": $minimal,
  "hideInFullscreen": false,
  "glassBackground": true,
  "bouncySpring": true
}
EOF
}

start_app() {
    local name="$1"
    local minimal="$2"
    shift 2
    stop_app
    case_dir="$work/$name"
    write_settings "$minimal"
    ready="$case_dir/ready"
    log="$case_dir/app.log"
    rm -f "$ready"
    HOME="$case_dir/home" \
    XDG_DATA_HOME="$case_dir/data" \
    XDG_CONFIG_HOME="$case_dir/config" \
    ASTER_READY_FILE="$ready" \
        "$exe" "$@" >"$log" 2>&1 &
    pid=$!
    for _ in $(seq 1 300); do
        [ -f "$ready" ] && return 0
        if ! kill -0 "$pid" 2>/dev/null; then
            echo "app exited before ready"
            cat "$log" 2>/dev/null || true
            exit 1
        fi
        sleep 0.05
    done
    echo "ready file not written"
    cat "$log" 2>/dev/null || true
    exit 1
}

# Restrict matches to the app launched by this script; a leftover instance must
# not satisfy or invalidate the probe.
find_app_visible() {
    local pattern="$1"
    local wid candidate
    for wid in $(xdotool search --onlyvisible --name "$pattern" 2>/dev/null); do
        candidate="$(xprop -id "$wid" _NET_WM_PID 2>/dev/null | sed -n 's/.*= //p' || true)"
        if [ "$candidate" = "${pid:-}" ]; then
            echo "$wid"
        fi
    done | tail -1
}

find_app_window() {
    local pattern="$1"
    local wid candidate
    for wid in $(xdotool search --name "$pattern" 2>/dev/null); do
        candidate="$(xprop -id "$wid" _NET_WM_PID 2>/dev/null | sed -n 's/.*= //p' || true)"
        if [ "$candidate" = "${pid:-}" ]; then
            echo "$wid"
        fi
    done | tail -1
}

wait_visible() {
    local pattern="$1"
    local wid=""
    for _ in $(seq 1 100); do
        wid="$(find_app_visible "$pattern")"
        if [ -n "$wid" ]; then
            echo "$wid"
            return 0
        fi
        sleep 0.05
    done
    return 1
}

wait_hidden() {
    local pattern="$1"
    for _ in $(seq 1 100); do
        [ -z "$(find_app_visible "$pattern")" ] && return 0
        sleep 0.05
    done
    return 1
}

read_geometry() {
    eval "$(xdotool getwindowgeometry --shell "$1")"
}

expect_size() {
    local wid="$1"
    local expected_width="$2"
    local expected_height="$3"
    local label="$4"
    read_geometry "$wid"
    if [ "$WIDTH" -lt $((expected_width - 2)) ] ||
       [ "$WIDTH" -gt $((expected_width + 2)) ] ||
       [ "$HEIGHT" -lt $((expected_height - 2)) ] ||
       [ "$HEIGHT" -gt $((expected_height + 2)) ]; then
        echo "$label size mismatch: got ${WIDTH}x${HEIGHT} expected ${expected_width}x${expected_height}"
        return 1
    fi
    return 0
}

expect_center() {
    local wid="$1"
    local screen_width="$2"
    local label="$3"
    read_geometry "$wid"
    local expected_x=$(((screen_width - WIDTH) / 2))
    [ "$X" -ge $((expected_x - 2)) ] &&
    [ "$X" -le $((expected_x + 2)) ] ||
        { echo "$label x mismatch: got $X expected $expected_x"; return 1; }
    [ "$Y" -ge -2 ] && [ "$Y" -le 2 ] ||
        { echo "$label y mismatch: got $Y expected 0"; return 1; }
    return 0
}

choose_away_position() {
    local screen_width="$1"
    local screen_height="$2"
    pointer_position
    away_x=10
    away_y=10
    if [ "$PTR_X" -lt $((screen_width / 2)) ]; then
        away_x=$((screen_width - 1100))
    fi
    if [ "$PTR_Y" -lt $((screen_height / 2)) ]; then
        away_y=$((screen_height - 600))
    fi
    if [ "$away_x" -lt 0 ]; then
        away_x=10
    fi
    if [ "$away_y" -lt 0 ]; then
        away_y=10
    fi
}

# Keep the window away from the real pointer while the app's startup snap and
# first hover tick run. Without this the stale XWayland pointer (which can sit
# inside the top-center island) expands the island before it can be measured.
settle_away() {
    local wid="$1"
    local screen_width="$2"
    local screen_height="$3"
    choose_away_position "$screen_width" "$screen_height"
    for _ in $(seq 1 70); do
        xdotool windowmove "$wid" "$away_x" "$away_y" 2>/dev/null || true
        sleep 0.003
    done
    sleep 0.35
}

wait_for_growth() {
    local wid="$1"
    local minimum_width="$2"
    local minimum_height="$3"
    local held=0
    local max_width=0
    local max_height=0
    for _ in $(seq 1 150); do
        read_geometry "$wid" 2>/dev/null || break
        # When KWin ignores XTest warps, keep the window under the real pointer
        # while measuring; the motion controller recenters it every frame.
        if [ "$pointer_warp" != "yes" ]; then
            xdotool windowmove "$wid" $((PTR_X - WIDTH / 2)) $((PTR_Y - HEIGHT / 2)) 2>/dev/null || true
            read_geometry "$wid" 2>/dev/null || break
        fi
        [ "$WIDTH" -gt "$max_width" ] && max_width="$WIDTH"
        [ "$HEIGHT" -gt "$max_height" ] && max_height="$HEIGHT"
        if [ "$WIDTH" -ge "$minimum_width" ] && [ "$HEIGHT" -ge "$minimum_height" ]; then
            held=$((held + 1))
            if [ "$held" -ge 15 ]; then
                echo "$WIDTH $HEIGHT"
                return 0
            fi
        else
            held=0
        fi
        sleep 0.02
    done
    echo "$max_width $max_height"
    return 1
}

probe_pointer_warp
if [ "$pointer_warp" = "yes" ]; then
    note "XTest pointer warps are honored; moving the real pointer."
else
    note "KWin ignores XTest pointer warps; moving the island window under the real pointer instead."
fi

screen_width="$(xdotool getdisplaygeometry | awk '{print $1}')"
screen_height="$(xdotool getdisplaygeometry | awk '{print $2}')"
expected_width=$(awk -v s="$scale" 'BEGIN {printf "%d", 300 * s}')
expected_height=$(awk -v s="$scale" 'BEGIN {printf "%d", 38 * s}')
minimal_width=$(awk -v s="$scale" 'BEGIN {printf "%d", 160 * s}')
minimal_height=$(awk -v s="$scale" 'BEGIN {printf "%d", 12 * s}')
expanded_width=$(awk -v s="$scale" 'BEGIN {printf "%d", 500 * s}')
expanded_height=$(awk -v s="$scale" 'BEGIN {printf "%d", 252 * s}')
effect_width=$(awk -v s="$scale" 'BEGIN {printf "%d", 760 * s}')
effect_height=$(awk -v s="$scale" 'BEGIN {printf "%d", 460 * s}')

# --- Baseline: 300x38 island, click does not take focus, effect window maps. ---
start_app baseline false
wid="$(wait_visible '^Aster Lab Avalonia$')" || fail "island window not found"
expect_center "$wid" "$screen_width" "island" ||
    fail "island is not positioned at the top center"

settle_away "$wid" "$screen_width" "$screen_height"
expect_size "$wid" "$expected_width" "$expected_height" "island" ||
    fail "island did not collapse after moving off the pointer"

# Place the collapsed island under the real pointer, then wait for a held
# expansion. On hosts that honor warps, move the pointer instead.
pointer_position
read_geometry "$wid"
if [ "$pointer_warp" = "yes" ]; then
    xdotool mousemove $((X + WIDTH / 2)) $((Y + HEIGHT / 2)) 2>/dev/null || true
else
    xdotool windowmove "$wid" $((PTR_X - WIDTH / 2)) $((PTR_Y - HEIGHT / 2)) 2>/dev/null || true
fi
growth_threshold_width=$((expected_width + (expanded_width - expected_width) / 4))
growth="$(wait_for_growth "$wid" "$growth_threshold_width" $((expected_height + 10)))" ||
    fail "island did not expand on hover after moving under the pointer"
note "island hovered to ${growth} (collapsed ${expected_width}x${expected_height}, expanded ${expanded_width}x${expanded_height})"

# Return the expanded island to the top center for the effect geometry and
# click checks. The motion controller centers the current (expanded) width.
xdotool windowmove "$wid" $(((screen_width - expanded_width) / 2)) 0 2>/dev/null || true
sleep 0.2

effect_wid="$(wait_visible '^Aster Effect Layer$')" || fail "effect window not found"

before="$(xdotool getactivewindow 2>/dev/null || true)"
pointer_position
read_geometry "$wid"
if [ "$pointer_warp" = "yes" ]; then
    xdotool mousemove $((X + WIDTH / 2)) $((Y + HEIGHT / 2))
    pill_click="yes"
elif [ "$PTR_X" -ge "$X" ] && [ "$PTR_X" -le $((X + WIDTH)) ] &&
     [ "$PTR_Y" -ge "$Y" ] && [ "$PTR_Y" -le $((Y + HEIGHT)) ]; then
    pill_click="yes"
else
    pill_click="no"
    focus_check="PARTIAL"
    echo "warning: the XTest pointer is not over the island; pill focus is not observable"
fi
if [ "$pill_click" = "yes" ]; then
    xdotool click 1 2>/dev/null || true
fi
sleep 0.4
after="$(xdotool getactivewindow 2>/dev/null || true)"
if [ -n "$after" ] && [ "$before" != "$after" ]; then
    if [ "$after" = "$wid" ] || [ "$after" = "$effect_wid" ]; then
        fail "pill click gave focus to the island or effect: $before -> $after"
    fi
    # A third window means the click leaked through the island. Only an empty
    # active window is genuinely unobservable under XWayland/KWin.
    fail "pill click moved focus to a third window: $before -> $after"
fi
if [ -z "$after" ]; then
    focus_check="PARTIAL"
    echo "warning: KWin cleared the active window after the pill click; focus hand-off not observable"
fi
if [ -z "$before" ]; then
    focus_check="PARTIAL"
    echo "warning: no active window before the pill click; focus hand-off not observable"
fi

expect_size "$effect_wid" "$effect_width" "$effect_height" "effect" ||
    fail "effect window size mismatch"
read_geometry "$effect_wid"
expected_effect_x=$(((screen_width - effect_width) / 2))
[ "$X" -ge $((expected_effect_x - 3)) ] && [ "$X" -le $((expected_effect_x + 3)) ] ||
    fail "effect x mismatch: got $X expected $expected_effect_x"
[ "$Y" -ge -3 ] && [ "$Y" -le 3 ] ||
    fail "effect y mismatch: got $Y expected 0"

spectacle -b -n -o "docs/results/screenshots/phase2/linux-window-${scale}x.png" 2>/dev/null || true

xmessage -geometry 300x200+50+300 -buttons OK "underneath" >/dev/null 2>&1 &
under_pid=$!
under_wid=""
for _ in $(seq 1 50); do
    under_wid="$(xdotool search --class Xmessage 2>/dev/null | tail -1 || true)"
    [ -n "$under_wid" ] && break
    sleep 0.1
done
[ -n "$under_wid" ] || fail "underlying xmessage window not found"
pointer_position
read_geometry "$under_wid"
under_click="no"
if [ "$pointer_warp" = "yes" ]; then
    xdotool mousemove $((X + WIDTH / 2)) $((Y + HEIGHT / 2))
    under_click="yes"
elif [ "$PTR_X" -ge "$X" ] && [ "$PTR_X" -le $((X + WIDTH)) ] &&
     [ "$PTR_Y" -ge "$Y" ] && [ "$PTR_Y" -le $((Y + HEIGHT)) ]; then
    under_click="yes"
fi
if [ "$under_click" = "yes" ]; then
    xdotool click 1 2>/dev/null || true
    sleep 0.4
    after_under="$(xdotool getactivewindow 2>/dev/null || true)"
    if [ "$after_under" != "$under_wid" ]; then
        if [ -n "$after_under" ]; then
            fail "underlying click moved focus to $after_under instead of $under_wid"
        fi
        # KDE Wayland may clear the active window when a synthetic click is not
        # delivered; only that case is unobservable.
        focus_check="PARTIAL"
        echo "warning: synthetic click left no active window; focus hand-off not observable"
    fi
else
    focus_check="PARTIAL"
    echo "warning: the XTest pointer is not over the underlying window; focus hand-off not observable"
fi
kill "$under_pid" 2>/dev/null || true
wait "$under_pid" 2>/dev/null || true
under_pid=""

stop_app

# --- Minimal notch: 160x12, hover expands it. ---
start_app minimal true
wid="$(wait_visible '^Aster Lab Avalonia$')" || fail "minimal island window not found"
settle_away "$wid" "$screen_width" "$screen_height"
expect_size "$wid" "$minimal_width" "$minimal_height" "minimal island" ||
    fail "minimal island is not 160x12 logical scaled"
effect_wid="$(wait_visible '^Aster Effect Layer$')" || fail "minimal effect window not found"

if [ "$pointer_warp" = "yes" ]; then
    read_geometry "$wid"
    xdotool mousemove $((X + WIDTH / 2)) $((Y + HEIGHT / 2))
else
    read_geometry "$wid"
    pointer_position
    xdotool windowmove "$wid" $((PTR_X - WIDTH / 2)) $((PTR_Y - HEIGHT / 2))
fi
minimum_width=$((minimal_width + (expanded_width - minimal_width) / 4))
minimum_height=$((minimal_height + 10))
growth="$(wait_for_growth "$wid" "$minimum_width" "$minimum_height")" ||
    fail "minimal island did not expand on hover (max ${growth})"
note "minimal island hovered to ${growth} (collapsed ${minimal_width}x${minimal_height}, expanded ${expanded_width}x${expanded_height})"
stop_app

# --- Settings open: effect must be unmapped, then return when settings close. ---
start_app settings false --settings
settings_wid="$(wait_visible '^Aster Settings$')" || fail "settings window not found"
effect_wid=""
for _ in $(seq 1 100); do
    effect_wid="$(find_app_window '^Aster Effect Layer$')"
    [ -n "$effect_wid" ] && break
    sleep 0.05
done
[ -n "$effect_wid" ] || fail "effect window was not created for the settings case"
if ! wait_hidden '^Aster Effect Layer$'; then
    fail "effect window is visible while settings are open"
fi
sleep 0.5
[ -z "$(find_app_visible '^Aster Effect Layer$')" ] ||
    fail "effect window became visible again while settings were open"

xdotool windowclose "$settings_wid" 2>/dev/null || true
wait_hidden '^Aster Settings$' || fail "settings window did not close"
effect_wid="$(wait_visible '^Aster Effect Layer$')" ||
    fail "effect window did not return after settings closed"
stop_app

if [ "$focus_check" = "PASS" ]; then
    echo "Linux window probe PASS at scale=$scale"
else
    echo "Linux window probe PARTIAL at scale=$scale (focus hand-off not observable)"
    exit 3
fi
