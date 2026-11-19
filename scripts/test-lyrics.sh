#!/usr/bin/env bash
# Runtime probe for the Phase 4B lyrics pane.
#
# Launches the fake MPRIS player and Aster inside a private session bus with a
# throwaway HOME/XDG tree, seeds the lyrics cache for the fixture track, opens
# the lyrics pane, and asserts the rendered copy with OCR.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exe="${1:-$root/src/Aster.Avalonia/Aster.Desktop.Linux/bin/Release/net10.0/Aster.Avalonia}"
out="$root/docs/results/screenshots/phase4b"
work="$(mktemp -d "${TMPDIR:-/tmp}/aster-lyrics.XXXXXX")"
pid=""
fake=""
bus_pid=""
private_bus=""
uinput_fifo="${ASTER_UINPUT_FIFO:-}"
uinput_fd=""

cleanup() {
    if [ -n "${uinput_fd:-}" ]; then
        printf 'quit\n' >&"${uinput_fd}" 2>/dev/null || true
        exec {uinput_fd}>&- 2>/dev/null || true
    fi
    [ -n "$fake" ] && kill "$fake" 2>/dev/null || true
    [ -n "$pid" ] && kill "$pid" 2>/dev/null || true
    [ -n "$bus_pid" ] && kill "$bus_pid" 2>/dev/null || true
    if [ "${KEEP_WORK:-0}" = 1 ]; then
        echo "work kept: $work" >&2
    else
        rm -rf "$work"
    fi
}
trap cleanup EXIT

init_uinput() {
    if [ -n "$uinput_fifo" ]; then
        [ -p "$uinput_fifo" ] || fail "uinput FIFO not found: $uinput_fifo"
        exec {uinput_fd}>"$uinput_fifo" || fail "could not open uinput FIFO"
    fi
}

pointer_move() {
    if [ -n "${uinput_fd:-}" ]; then
        printf 'move %s %s\n' "$1" "$2" >&"${uinput_fd}"
        sleep 0.2
    else
        xdotool mousemove "$1" "$2" 2>/dev/null || true
    fi
}

pointer_click_at() {
    if [ -n "${uinput_fd:-}" ]; then
        printf 'move-click %s %s\n' "$1" "$2" >&"${uinput_fd}"
        sleep 0.45
    else
        xdotool mousemove "$1" "$2" 2>/dev/null || true
        sleep 0.1
        xdotool click 1 2>/dev/null || true
        sleep 0.2
    fi
}

fail() {
    echo "FAIL: $*" >&2
    [ -s "$work/app.log" ] && tail -n 30 "$work/app.log" >&2
    exit 1
}

for tool in xdotool tesseract import python3; do
    command -v "$tool" >/dev/null || fail "$tool is required"
done
python3 -c 'import dbus_next, PIL' >/dev/null 2>&1 ||
    fail "python3 dbus-next and Pillow are required"
[ -x "$exe" ] || fail "executable not found: $exe"

mkdir -p "$out" "$work/home" "$work/data" "$work/config"

# Keep the fake player isolated from the real desktop's MPRIS players.
mapfile -t bus < <(dbus-daemon --session --fork --print-address=1 --print-pid=1)
private_bus="${bus[0]:-}"
bus_pid="${bus[1]:-}"
[ -n "$private_bus" ] && [ -n "$bus_pid" ] ||
    fail "could not start a private session bus"

key='aurora nights|capture fixture|capture fixture|180'
hash="$(printf '%s' "$key" | sha256sum | cut -c1-24)"
mkdir -p "$work/data/AsterLab/Avalonia/lyrics"
cat >"$work/data/AsterLab/Avalonia/lyrics/$hash.json" <<'JSON'
{"savedAt":"2026-11-18T00:00:00Z","document":{"isSynced":true,"isInstrumental":false,"plainLyrics":null,"lines":[{"timeSeconds":0,"text":"First light on the wire"},{"timeSeconds":4,"text":"A quiet room, a steady pulse"},{"timeSeconds":8,"text":"We keep the signal alive"},{"timeSeconds":12,"text":"Aster, stay with me tonight"}],"trackTitle":"Aurora Nights","artistName":"Capture Fixture","source":"LRCLIB"}}
JSON

DBUS_SESSION_BUS_ADDRESS="$private_bus" \
python3 "$root/tools/fake-mpris-player.py" \
    --title "Aurora Nights" --artist "Capture Fixture" \
    --album "Capture Fixture" --duration 180 \
    --art "file://$root/artifacts/capture-env/media/cover.png" \
    >"$work/fake.log" 2>&1 &
fake=$!
sleep 0.8
kill -0 "$fake" 2>/dev/null || { cat "$work/fake.log" >&2; fail "fake player exited"; }

HOME="$work/home" XDG_DATA_HOME="$work/data" XDG_CONFIG_HOME="$work/config" \
DBUS_SESSION_BUS_ADDRESS="$private_bus" ASTER_READY_FILE="$work/ready" \
    "$exe" >"$work/app.log" 2>&1 &
pid=$!

for _ in $(seq 1 200); do
    [ -f "$work/ready" ] && break
    kill -0 "$pid" 2>/dev/null || fail "app exited before the ready file"
    sleep 0.05
done
[ -f "$work/ready" ] || fail "ready file was not written"
sleep 2

find_island() {
    local candidate
    for candidate in $(xdotool search --name '^Aster Lab Avalonia$' 2>/dev/null); do
        if [ "$(xprop -id "$candidate" _NET_WM_PID 2>/dev/null | sed -n 's/.*= //p')" = "$pid" ]; then
            echo "$candidate"
            return 0
        fi
    done
    return 1
}

wid="$(find_island)" || fail "island window not found"

expand_island() {
    for attempt in 1 2 3 4 5 6; do
        eval "$(xdotool getwindowgeometry --shell "$wid")"
        if [ "$WIDTH" -ge 950 ] && [ "$HEIGHT" -ge 480 ]; then
            return 0
        fi
        # XTest warps are ignored by KWin, so the uinput path moves the real
        # pointer. The fallback moves the window under the existing pointer.
        if [ -n "${uinput_fd:-}" ]; then
            pointer_move $((X + WIDTH / 2)) $((Y + HEIGHT / 2))
        else
            eval "$(xdotool getmouselocation --shell | sed 's/^/PTR_/')"
            xdotool windowmove "$wid" $((PTR_X - WIDTH / 2)) $((PTR_Y - HEIGHT / 2)) 2>/dev/null || true
            xdotool click --window "$wid" 1 2>/dev/null || true
        fi
        sleep 0.25
    done
    eval "$(xdotool getwindowgeometry --shell "$wid")"
    fail "island did not expand (got ${WIDTH}x${HEIGHT})"
}

init_uinput
expand_island
eval "$(xdotool getwindowgeometry --shell "$wid")"

# The lyrics button sits at the right edge of the 464 DIP music column. The
# default is for a 200% 1000x504 window; override with LYRICS_CLICK_X/Y after
# checking a screenshot on a different scale.
click_x="${LYRICS_CLICK_X:-938}"
click_y="${LYRICS_CLICK_Y:-297}"
pointer_click_at $((X + click_x)) $((Y + click_y))
sleep 0.9

shot="$out/lyrics-synced.png"
rm -f "$shot"
if command -v import >/dev/null 2>&1; then
    import -window "$wid" "$shot" 2>/dev/null || true
fi
if [ ! -s "$shot" ] && command -v spectacle >/dev/null 2>&1; then
    spectacle -a -b -n -o "$shot" >/dev/null 2>&1 || true
fi
[ -s "$shot" ] || fail "could not capture the island"

text="$(tesseract "$shot" stdout 2>/dev/null | tr '\n' ' ')"
grep -Fqi "LYRICS" <<<"$text" ||
    fail "lyrics pane not visible: $text"
grep -Fqi "First light on the wire" <<<"$text" ||
    fail "fixture lyric not visible: $text"

echo "Lyrics probe PASS"
echo "  screenshot: $shot"
echo "  window: ${WIDTH}x${HEIGHT}"
echo "  click: $click_x,$click_y"
