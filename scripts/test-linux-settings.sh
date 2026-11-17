#!/usr/bin/env bash
# Runtime gate for the Phase 4A settings window.
#
# Launches the built app with a throwaway HOME so the probe never reads or
# writes the real user settings. The window is captured at native XWayland
# resolution with `import -window` (crisper than a scaled full-screen capture)
# and the text is asserted with tesseract. `spectacle` is the fallback capture
# for hosts without ImageMagick or X11 window capture.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exe="${1:-$root/src/Aster.Avalonia/Aster.Desktop.Linux/bin/Release/net10.0/Aster.Avalonia}"
out_dir="$root/docs/results/screenshots/phase4a"
work="$(mktemp -d "${TMPDIR:-/tmp}/aster-settings.XXXXXX")"
ready="$work/ready"
log="$work/app.log"
pid=""
wid=""

mkdir -p "$out_dir"

cleanup() {
    if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
        kill "$pid" 2>/dev/null || true
        for _ in $(seq 1 40); do
            kill -0 "$pid" 2>/dev/null || break
            sleep 0.05
        done
        kill -9 "$pid" 2>/dev/null || true
        wait "$pid" 2>/dev/null || true
    fi
    rm -rf "$work"
}
trap cleanup EXIT

fail() {
    echo "FAIL: $*" >&2
    [ -s "$log" ] && tail -n 30 "$log" >&2
    exit 1
}

# Resolve the runtime before HOME is replaced: the apphost uses DOTNET_ROOT.
dotnet_root="${DOTNET_ROOT:-}"
if [ -z "$dotnet_root" ] && [ -d "$HOME/.dotnet" ]; then
    dotnet_root="$HOME/.dotnet"
fi
if [ -n "$dotnet_root" ]; then
    export DOTNET_ROOT="$dotnet_root"
    export PATH="$dotnet_root:$PATH"
fi

command -v xdotool >/dev/null || fail "xdotool is required"
command -v tesseract >/dev/null || fail "tesseract is required"
python3 -c 'import PIL' >/dev/null 2>&1 ||
    fail "python3 Pillow is required for the disabled-toggle luminance check"
[ -x "$exe" ] || fail "executable not found: $exe"

mkdir -p "$work/home" "$work/data" "$work/config"

HOME="$work/home" \
XDG_DATA_HOME="$work/data" \
XDG_CONFIG_HOME="$work/config" \
ASTER_READY_FILE="$ready" \
    "$exe" --settings >"$log" 2>&1 &
pid=$!

for _ in $(seq 1 200); do
    [ -f "$ready" ] && break
    kill -0 "$pid" 2>/dev/null || fail "settings app exited before the ready file"
    sleep 0.05
done
[ -f "$ready" ] || fail "ready file was not written"

find_app_window() {
    local pattern="$1"
    local candidate
    for candidate in $(xdotool search --name "$pattern" 2>/dev/null); do
        if [ "$(xprop -id "$candidate" _NET_WM_PID 2>/dev/null | sed -n 's/.*= //p')" = "$pid" ]; then
            echo "$candidate"
        fi
    done | tail -1
}

for _ in $(seq 1 120); do
    wid="$(find_app_window '^Aster Settings$')"
    [ -n "$wid" ] && break
    sleep 0.05
done
[ -n "$wid" ] || fail "settings window not found"

title="$(xdotool getwindowname "$wid" 2>/dev/null || true)"
[ "$title" = "Aster Settings" ] ||
    fail "window title is '$title', expected exactly 'Aster Settings'"

# Bring the window into focus so Tab and Space reach it.
xdotool windowactivate --sync "$wid" 2>/dev/null || true
sleep 0.3

shot_path=""
capture() {
    local name="$1"
    shot_path="$out_dir/$name"
    if command -v import >/dev/null 2>&1; then
        if import -window "$wid" "$shot_path" 2>/dev/null && [ -s "$shot_path" ]; then
            return 0
        fi
    fi
    # Active-window capture through KWin; OCR still works, just at 1x.
    if command -v spectacle >/dev/null 2>&1; then
        spectacle -a -b -n -o "$shot_path" >/dev/null 2>&1 || true
    fi
    [ -s "$shot_path" ] || fail "could not capture the settings window"
}

ocr() {
    tesseract "$1" stdout 2>/dev/null | tr '\n' ' ' | tr -s '[:space:]' ' ' | sed 's/^ //; s/ $//'
}

require_text() {
    local text="$1"
    local label="$2"
    shift 2
    local needle
    for needle in "$@"; do
        grep -Fqi -- "$needle" <<<"$text" ||
            fail "$label: missing '$needle' in OCR text: $text"
    done
}

# Press Tab+Space until the requested page marker appears. RadioButton Space
# selects the focused nav item; the checked General item is a no-op.
show_page() {
    local marker="$1"
    local name="$2"
    local attempts="$3"
    local text=""
    for _ in $(seq 1 "$attempts"); do
        kill -0 "$pid" 2>/dev/null || fail "app exited while navigating to $name"
        xdotool key --window "$wid" Tab 2>/dev/null || true
        sleep 0.08
        xdotool key --window "$wid" space 2>/dev/null || true
        sleep 0.45
        capture "$name"
        text="$(ocr "$shot_path")"
        if grep -Fqi -- "$marker" <<<"$text"; then
            printf '%s' "$text"
            return 0
        fi
    done
    printf '%s' "$text"
    return 1
}

# --- General (startup page). ---
capture settings-general.png
general_text="$(ocr "$shot_path")"

grep -Fq "Aster Settings" <<<"$general_text" ||
    fail "General: title text 'Aster Settings' not found: $general_text"
require_text "$general_text" "General/nav" \
    "General" "Appearance" "Lyrics" "Focus" "Water" "About"
require_text "$general_text" "General toggles" \
    "Start with system" "Hide in fullscreen"

# --- Appearance. ---
appearance_text="$(show_page "Island shape, surface, motion, and overlays." settings-appearance.png 6)" ||
    fail "Appearance page did not open (Tab/Space navigation failed)"
require_text "$appearance_text" "Appearance" \
    "Minimal notch" "Show mascot" "Glass background" "Bouncy open/close" \
    "Volume overlay" "Brightness overlay"

# Disabled toggles must render dimmer than enabled ones. Read the label text
# color from the TSV boxes instead of asserting only that the labels exist.
python3 - "$out_dir/settings-appearance.png" <<'PY' || fail "overlay toggles do not look disabled"
import subprocess
import sys
from PIL import Image

path = sys.argv[1]
tsv = subprocess.run(
    ["tesseract", path, "stdout", "tsv"],
    capture_output=True,
    text=True,
    check=False,
).stdout
words = []
for line in tsv.splitlines()[1:]:
    cols = line.split("\t")
    if len(cols) >= 12 and cols[11].strip():
        words.append(
            (cols[11].strip(), int(cols[6]), int(cols[7]), int(cols[8]), int(cols[9]))
        )

image = Image.open(path).convert("RGB")


def brightest(word, occurrence):
    matches = [box for box in words if box[0] == word]
    if len(matches) < occurrence:
        raise SystemExit(f"OCR did not find word {word!r} occurrence {occurrence}")
    _, x, y, w, h = matches[occurrence - 1]
    return image.crop((x, y, x + w, y + h)).convert("L").getextrema()[1]


enabled = min(brightest("Minimal", 1), brightest("Glass", 1), brightest("Bouncy", 1))
volume = brightest("Volume", 2)      # first occurrence is the explanatory sentence
brightness = brightest("Brightness", 1)
print(
    f"label luminance: enabled={enabled:.0f} volume={volume:.0f} brightness={brightness:.0f}"
)
if enabled < 200:
    raise SystemExit(f"enabled labels are unexpectedly dim ({enabled:.0f})")
if volume > 160 or brightness > 160:
    raise SystemExit(
        f"disabled overlay labels are not dim (volume={volume:.0f}, brightness={brightness:.0f})"
    )
PY

# --- About. ---
about_text="$(show_page "Storage, version, and application information." settings-about.png 6)" ||
    fail "About page did not open (Tab/Space navigation failed)"
require_text "$about_text" "About page" "Open data folder"
compact_text="$(printf '%s' "$about_text" | tr -d '[:space:]')"
compact_path="$(printf '%s' "$work/data/AsterLab/Avalonia" | tr -d '[:space:]')"
grep -Fq "$compact_path" <<<"$compact_text" ||
    fail "About: data path '$work/data/AsterLab/Avalonia' not found in OCR text: $about_text"

echo "Linux settings probe PASS"
echo "  title: $title"
echo "  screenshots: $out_dir/settings-{general,appearance,about}.png"
