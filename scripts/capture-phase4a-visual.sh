#!/usr/bin/env bash
# Capture Phase 4A shell surfaces at 125% against the WPF reference crops.
# This script changes the KDE output scale and restores it on exit. It refuses
# to run unless ASTER_CAPTURE_OK=1, because the desktop will resize briefly.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exe="${1:-$root/src/Aster.Avalonia/Aster.Desktop.Linux/bin/Release/net10.0/Aster.Avalonia}"
out="$root/docs/results/screenshots/phase4a"
work="$(mktemp -d "${TMPDIR:-/tmp}/aster-capture.XXXXXX")"
wallpaper="$root/artifacts/capture-env/wallpaper-dark-101014.png"
cover="$root/artifacts/capture-env/media/cover.png"
pid=""
fake=""
private_bus=""
bus_pid=""

[ "${ASTER_CAPTURE_OK:-0}" = "1" ] || {
    echo "Set ASTER_CAPTURE_OK=1 to allow the 125% scale switch." >&2
    exit 2
}

for tool in kscreen-doctor spectacle plasma-apply-wallpaperimage \
            import identify convert xdotool gdbus dbus-daemon python3; do
    command -v "$tool" >/dev/null || { echo "missing: $tool" >&2; exit 2; }
done
python3 -c 'import dbus_next' >/dev/null 2>&1 ||
    { echo "python3-dbus-next is required" >&2; exit 2; }
[ -x "$exe" ] || { echo "missing executable: $exe" >&2; exit 2; }
[ -f "$wallpaper" ] || { echo "missing wallpaper: $wallpaper" >&2; exit 2; }
[ -f "$cover" ] || { echo "missing cover: $cover" >&2; exit 2; }

screen="$(kscreen-doctor -o | sed 's/\x1b\[[0-9;]*[mK]//g')"
# KWin's XWayland scale follows the highest output scale, so a mixed-DPI host
# keeps 200% unless every enabled output moves to 125%. Save and restore each.
outputs=()
scales=()
while read -r name scale; do
    [ -n "$name" ] && [ -n "$scale" ] || continue
    outputs+=("$name")
    scales+=("$scale")
done < <(awk '/^Output:/{name=$3} /^[[:space:]]*Scale:/{if(name!="") print name, $2; name=""}' <<<"$screen")
[ "${#outputs[@]}" -gt 0 ] ||
    { echo "could not parse kscreen-doctor output" >&2; exit 2; }
wallpaper_before="$(python3 - "$HOME/.config/plasma-org.kde.plasma.desktop-appletsrc" <<'PY'
import sys, urllib.parse
try:
    for line in open(sys.argv[1], encoding="utf-8"):
        if line.startswith("Image=file://"):
            print(urllib.parse.unquote(line.strip()[len("Image=file://"):]))
            break
except OSError:
    pass
PY
)"

real_home="$HOME"
real_config="${XDG_CONFIG_HOME:-$HOME/.config}"
real_data="${XDG_DATA_HOME:-$HOME/.local/share}"

cleanup() {
    [ -n "$fake" ] && kill "$fake" 2>/dev/null || true
    if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
        kill "$pid" 2>/dev/null || true
        wait "$pid" 2>/dev/null || true
    fi
    [ -n "$bus_pid" ] && kill "$bus_pid" 2>/dev/null || true
    for i in "${!outputs[@]}"; do
        kscreen-doctor "output.${outputs[$i]}.scale.${scales[$i]}" >/dev/null 2>&1 || true
    done
    if [ -n "$wallpaper_before" ]; then
        HOME="$real_home" XDG_CONFIG_HOME="$real_config" XDG_DATA_HOME="$real_data" \
            plasma-apply-wallpaperimage "$wallpaper_before" >/dev/null 2>&1 ||
            echo "warning: wallpaper restore failed" >&2
    fi
    rm -rf "$work"
}
trap cleanup EXIT

# Isolate MPRIS from the desktop's real players (browser, music apps). The app
# and fake player share this private bus; KDE services keep the real one.
mapfile -t bus < <(dbus-daemon --session --fork --print-address=1 --print-pid=1)
private_bus="${bus[0]}"
bus_pid="${bus[1]}"
[ -n "$private_bus" ] && [ -n "$bus_pid" ] ||
    { echo "could not start a private DBus session" >&2; exit 2; }

mkdir -p "$out" "$work/home" "$work/data" "$work/config"
for name in "${outputs[@]}"; do
    kscreen-doctor "output.${name}.scale.1.25" >/dev/null
done
sleep 2
plasma-apply-wallpaperimage "$wallpaper" >/dev/null

dotnet_root="${DOTNET_ROOT:-}"
if [ -z "$dotnet_root" ] && [ -d "$HOME/.dotnet" ]; then
    dotnet_root="$HOME/.dotnet"
fi
export DOTNET_ROOT="$dotnet_root"
if [ -n "$dotnet_root" ]; then
    export PATH="$dotnet_root:$PATH"
fi

export HOME="$work/home"
export XDG_DATA_HOME="$work/data"
export XDG_CONFIG_HOME="$work/config"
export ASTER_READY_FILE="$work/ready"

find_island() {
    local wid geometry WIDTH HEIGHT
    for wid in $(xdotool search --name '^Aster Lab Avalonia$' 2>/dev/null); do
        geometry="$(xdotool getwindowgeometry --shell "$wid" 2>/dev/null || true)"
        eval "$geometry"
        case "${WIDTH}x${HEIGHT}" in
            375x48|625x315|200x15)
                echo "$wid"
                return 0
                ;;
        esac
    done
    return 1
}

wait_ready() {
    for _ in $(seq 1 200); do
        [ -f "$ASTER_READY_FILE" ] && return 0
        kill -0 "$pid" 2>/dev/null || return 1
        sleep 0.05
    done
    return 1
}

park_pointer() {
    # Keep the real pointer off the island; KWin ignores XTest warps, so the
    # controller's polling sees the actual pointer wherever it already is.
    xdotool mousemove 10 10 2>/dev/null || true
    sleep 0.2
}

# KWin recenters the island on every animation frame, and synthetic pointer
# warps are ignored. Move the window under the real pointer, catch the
# expansion a few frames before it settles, and grab the window itself. The
# window image is then composited onto the 125% wallpaper crop, so the panel
# and other windows never leak into the reference frame.
expand_island() {
    local wid="$1" geometry attempt
    eval "$(xdotool getmouselocation --shell | sed 's/^/PTR_/')"
    for attempt in 1 2 3; do
        eval "$(xdotool getwindowgeometry --shell "$wid")"
        xdotool windowmove "$wid" $(( PTR_X - WIDTH / 2 )) $(( PTR_Y - HEIGHT / 2 ))
        for _ in $(seq 1 80); do
            geometry="$(xdotool getwindowgeometry --shell "$wid" 2>/dev/null || true)"
            eval "$geometry"
            if [ "$WIDTH" -ge 620 ] && [ "$HEIGHT" -ge 310 ]; then
                return 0
            fi
            sleep 0.02
        done
    done
    echo "island did not expand on hover" >&2
    exit 1
}

capture_island() {
    local name="$1" raw="$work/window.png"
    local target="$out/avalonia-125-${name}.png"
    rm -f "$raw"
    import -window "$wid" "$raw" || { echo "window capture failed" >&2; exit 1; }
    python3 - "$raw" "$wallpaper" "$target" <<'PY'
import sys
from PIL import Image, ImageChops, ImageDraw

window = Image.open(sys.argv[1]).convert("RGB")

# `import -window` renders transparent window edge pixels as pure black. Trim
# those rows/columns, but never trim the all-black minimal notch.
black = Image.new("RGB", window.size, (0, 0, 0))
bbox = ImageChops.difference(window, black).getbbox()
if bbox and (bbox[2] - bbox[0]) >= 10 and (bbox[3] - bbox[1]) >= 5:
    window = window.crop(bbox)

background = Image.open(sys.argv[2]).convert("RGB")
width, height = window.size
if width > 950 or height > 575:
    raise SystemExit(f"window is larger than the reference crop: {width}x{height}")

canvas = background.crop(
    ((background.width - 950) // 2, 0,
     (background.width - 950) // 2 + 950, 575)
)
x = (950 - width) // 2
y = 0

# The island has square top corners and a 22 DIP bottom radius.
radius = min(round(22 * 1.25), height)
mask = Image.new("L", (width, height), 0)
ImageDraw.Draw(mask).rounded_rectangle(
    (0, 0, width - 1, height - 1),
    radius=radius,
    fill=255,
    corners=(False, False, True, True),
)
canvas.paste(window, (x, y), mask)
canvas.save(sys.argv[3])
print(f"{sys.argv[3]}: window={width}x{height} at {x},{y}")
PY
}

start_app() {
    DBUS_SESSION_BUS_ADDRESS="$private_bus" "$exe" >"$work/app.log" 2>&1 &
    pid=$!
    wait_ready || { tail -n 30 "$work/app.log" >&2; exit 1; }
    sleep 0.8
}

player_call() {
    DBUS_SESSION_BUS_ADDRESS="$private_bus" gdbus call \
        --session --dest org.mpris.MediaPlayer2.FakePlayer \
        --object-path /org/mpris/MediaPlayer2 \
        --method org.mpris.MediaPlayer2.Player.PlayPause >/dev/null
}

# Clock (no media).
start_app
wid="$(find_island)" || { echo "island window 950x575 not found" >&2; exit 1; }
park_pointer
capture_island "compact-clock"

# Compact media playing.
DBUS_SESSION_BUS_ADDRESS="$private_bus" \
python3 "$root/tools/fake-mpris-player.py" \
    --title "Aurora Nights" --artist "Capture Fixture" \
    --album "Capture Fixture" --duration 180 \
    --art "file://$cover" >"$work/fake.log" 2>&1 &
fake=$!
kill -0 "$fake" 2>/dev/null || { cat "$work/fake.log" >&2; exit 1; }
sleep 1.5
capture_island "compact-media-playing"

# Compact media paused.
player_call
sleep 1.0
capture_island "compact-media-paused"

# Expanded playing.
player_call
sleep 0.4
expand_island "$wid"
capture_island "expanded-playing"

# Expanded paused.
player_call
sleep 0.4
expand_island "$wid"
capture_island "expanded-paused"

# Expanded empty.
kill "$fake" 2>/dev/null || true
wait "$fake" 2>/dev/null || true
fake=""
sleep 0.6
expand_island "$wid"
capture_island "expanded-empty"

# Minimal notch: restart with a seeded schema-2 settings file.
kill "$pid" 2>/dev/null || true
wait "$pid" 2>/dev/null || true
rm -f "$ASTER_READY_FILE"
mkdir -p "$work/data/AsterLab/Avalonia"
printf '{"schemaVersion":2,"minimalNotch":true}' >"$work/data/AsterLab/Avalonia/settings.json"
start_app
wid="$(find_island)" || { echo "minimal island window not found" >&2; exit 1; }
park_pointer
capture_island "minimal-notch"

echo "Captured:"
ls -1 "$out"/avalonia-125-*.png
