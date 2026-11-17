#!/usr/bin/env bash
# Runtime gate for the Phase 4A tray item and its DBusMenu.
#
# Self-contained: when ASTER_PID is not supplied and no Aster process is
# running, the script launches the built executable under a throwaway HOME,
# waits for the ready file, runs the assertions, and stops the process again.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exe="${1:-$root/src/Aster.Avalonia/Aster.Desktop.Linux/bin/Release/net10.0/Aster.Avalonia}"
out_dir="$root/docs/results/screenshots/phase4a"
work="$(mktemp -d "${TMPDIR:-/tmp}/aster-tray.XXXXXX")"
ready="$work/ready"
log="$work/app.log"
pid="${ASTER_PID:-}"
launched=""
name=""

mkdir -p "$out_dir"

cleanup() {
    if [ -n "$launched" ] && kill -0 "$launched" 2>/dev/null; then
        kill "$launched" 2>/dev/null || true
        for _ in $(seq 1 40); do
            kill -0 "$launched" 2>/dev/null || break
            sleep 0.05
        done
        kill -9 "$launched" 2>/dev/null || true
        wait "$launched" 2>/dev/null || true
    fi
    rm -rf "$work"
}
trap cleanup EXIT

fail() {
    echo "FAIL: $*" >&2
    [ -s "$log" ] && tail -n 30 "$log" >&2
    exit 1
}

command -v busctl >/dev/null || fail "busctl is required"
command -v gdbus >/dev/null || fail "gdbus is required"
command -v python3 >/dev/null || fail "python3 is required"

if [ -z "$pid" ] && command -v pgrep >/dev/null 2>&1; then
    # A pgrep match can be this script or a shell whose command line contains
    # the executable path; require a process whose real executable is the app.
    for candidate in $(pgrep -f 'Aster.Avalonia' 2>/dev/null || true); do
        exe_link="$(readlink -f "/proc/$candidate/exe" 2>/dev/null || true)"
        if [ "$(basename "$exe_link")" = "Aster.Avalonia" ]; then
            pid="$candidate"
            break
        fi
    done
fi

if [ -z "$pid" ]; then
    [ -x "$exe" ] || fail "executable not found: $exe"
    dotnet_root="${DOTNET_ROOT:-}"
    if [ -z "$dotnet_root" ] && [ -d "$HOME/.dotnet" ]; then
        dotnet_root="$HOME/.dotnet"
    fi
    if [ -n "$dotnet_root" ]; then
        export DOTNET_ROOT="$dotnet_root"
        export PATH="$dotnet_root:$PATH"
    fi

    mkdir -p "$work/home/.local/share/AsterLab/Avalonia" "$work/home/.config/autostart" "$work/config"
    # The app reconciles startWithWindows with the real autostart entry on load,
    # so seed both or the start item is forced back to unchecked.
    cat >"$work/home/.config/autostart/aster-lab-avalonia.desktop" <<'DESKTOP'
[Desktop Entry]
Type=Application
Name=Aster Lab Avalonia
Exec="Aster.Avalonia"
Terminal=false
X-GNOME-Autostart-enabled=true
DESKTOP
    cat >"$work/home/.local/share/AsterLab/Avalonia/settings.json" <<'JSON'
{"schemaVersion":2,"startWithWindows":true,"minimalNotch":true,"hideInFullscreen":true,
 "glassBackground":true,"bouncySpring":true,"showMascot":false,"volumeToast":true,
 "brightnessToast":true,"lyricsEnabled":true,"lyricsHighlightColor":"#7C9CFF",
 "waterReminderEnabled":false,"waterIntervalMinutes":60,"waterActiveStartHour":8,
 "waterActiveEndHour":22,"waterDailyGoal":8,"waterGlassesToday":0,"waterDate":"",
 "cameraMirror":true,"focusMinutes":25,"shortBreakMinutes":5,"longBreakMinutes":15,
 "focusChime":true}
JSON

    HOME="$work/home" \
    XDG_DATA_HOME="$work/home/.local/share" \
    XDG_CONFIG_HOME="$work/config" \
    ASTER_READY_FILE="$ready" \
        "$exe" >"$log" 2>&1 &
    launched=$!
    pid=$launched
    for _ in $(seq 1 200); do
        [ -f "$ready" ] && break
        kill -0 "$pid" 2>/dev/null || fail "tray app exited before the ready file"
        sleep 0.05
    done
    [ -f "$ready" ] || fail "tray app did not write the ready file"
fi

for _ in $(seq 1 80); do
    name="$(busctl --user list 2>/dev/null |
        awk -v p="$pid" '$1 ~ ("org\\.kde\\.StatusNotifierItem-" p "-") && !found {print $1; found=1}')"
    [ -n "$name" ] && break
    sleep 0.1
done
[ -n "$name" ] || fail "Aster SNI item for pid $pid not found"

sni=""
for _ in $(seq 1 40); do
    sni="$(busctl --user introspect "$name" /StatusNotifierItem 2>/dev/null || true)"
    grep -q 'org.kde.StatusNotifierItem' <<<"$sni" && break
    sleep 0.1
done
grep -q 'org.kde.StatusNotifierItem' <<<"$sni" ||
    fail "StatusNotifierItem interface missing on $name"

sni_title="$(busctl --user get-property "$name" /StatusNotifierItem \
    org.kde.StatusNotifierItem Title 2>/dev/null | sed -n 's/^s "\(.*\)"$/\1/p')"
[ "$sni_title" = "Aster Lab Avalonia" ] ||
    fail "SNI title is '$sni_title', expected 'Aster Lab Avalonia'"

menu="$(busctl --user get-property "$name" /StatusNotifierItem \
    org.kde.StatusNotifierItem Menu 2>/dev/null | sed -n 's/^o "\(.*\)"$/\1/p')"
[ -n "$menu" ] || fail "SNI Menu path missing"

menu_info="$(busctl --user introspect "$name" "$menu" 2>/dev/null || true)"
grep -q 'com.canonical.dbusmenu' <<<"$menu_info" ||
    fail "DBusMenu interface missing at $menu"

layout="$(gdbus call --session --dest "$name" --object-path "$menu" \
    --method com.canonical.dbusmenu.GetLayout -- 0 -1 "[]" 2>/dev/null)" ||
    fail "GetLayout failed for $name $menu"

expected_mode="present"
if [ -n "$launched" ]; then
    expected_mode="seeded"
fi

MENU_LAYOUT="$layout" MENU_MODE="$expected_mode" python3 - <<'PY' || fail "DBusMenu assertions failed"
import os
import re
import sys

layout = os.environ["MENU_LAYOUT"]
mode = os.environ["MENU_MODE"]

items = re.findall(r"<\((\d+), \{(.*?)\}, @av \[\]\)>", layout, re.S)
if len(items) != 8:
    print(f"expected 8 DBusMenu items, found {len(items)}: {layout}")
    sys.exit(1)


def prop(props, key):
    match = re.search(r"'" + key + r"': <([^>]*)>", props)
    return match.group(1).strip("'") if match else None


labels = []
states = {}
for item_id, props in items:
    label = prop(props, "label") or ""
    labels.append(label)
    if prop(props, "toggle-type") == "checkmark":
        state = prop(props, "toggle-state")
        if state not in ("0", "1"):
            print(f"toggle item {label!r} has no valid toggle-state: {props}")
            sys.exit(1)
        states[label] = int(state)

expected_labels = [
    None,  # dynamic Show/Hide Aster
    "Start with system",
    "Minimal notch",
    "Show mascot",
    "Hide in fullscreen",
    "Settings...",
    "About Aster",
    "Exit",
]
if labels[0] not in ("Hide Aster", "Show Aster"):
    print(f"dynamic item label is {labels[0]!r}, expected 'Hide Aster' or 'Show Aster'")
    sys.exit(1)
if labels[1:] != expected_labels[1:]:
    print(f"unexpected DBusMenu labels: {labels}")
    sys.exit(1)

for label in ("Start with system", "Minimal notch", "Show mascot", "Hide in fullscreen"):
    if label not in states:
        print(f"{label!r} is not exposed as a toggle: {states}")
        sys.exit(1)

if mode == "seeded":
    expected_states = {
        "Start with system": 1,
        "Minimal notch": 1,
        "Show mascot": 0,
        "Hide in fullscreen": 1,
    }
    if states != expected_states:
        print(f"toggle states are {states}, expected {expected_states}")
        sys.exit(1)

print(f"labels: {labels}")
print(f"toggle states: {states}")
PY

spectacle -b -n -o "$out_dir/linux-tray.png" >/dev/null 2>&1 ||
    echo "warning: tray screenshot failed (spectacle)"

echo "Linux tray probe PASS ($name)"
echo "  title: $sni_title"
echo "  screenshot: $out_dir/linux-tray.png"
