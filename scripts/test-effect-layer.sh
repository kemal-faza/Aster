#!/usr/bin/env bash
# Synthetic X11 gate for the effect-layer spike (A1).
#
# Click-through proof under Xvfb:
#   - effect window (760x460, centered) has an empty X11 input shape
#   - a click at 960,400 (inside the effect, outside the island) MUST reach the
#     underlying click receiver
#   - a click at 960,20 (inside the island) MUST NOT reach that receiver
#
# The underlying receiver is a small python3/tkinter window at 500x400+600+250
# because `xev` is not installed on the Fedora host, and system packages must not
# be installed for this spike. It logs every ButtonPress to a file.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
spike="$root/spike/phase4-effect-layer/EffectLayerSpike.csproj"
tmp="$(mktemp -d)"
trap 'kill "${SPIKE_PID:-}" "${RECEIVER_PID:-}" "${XVFB_PID:-}" 2>/dev/null || true; rm -rf "$tmp"' EXIT

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

# Do not fake the gate when a dependency is missing.
missing=()
for tool in Xvfb xdotool python3; do
  command -v "$tool" >/dev/null 2>&1 || missing+=("$tool")
done
if ! python3 -c "import tkinter" >/dev/null 2>&1; then
  missing+=("python3-tkinter")
fi
if [ "${#missing[@]}" -gt 0 ]; then
  echo "BLOCKED: missing dependency: ${missing[*]}"
  exit 2
fi

dotnet build "$spike" -c Release >/tmp/effect-spike-build.log

if DISPLAY=:99 xdotool getdisplaygeometry >/dev/null 2>&1; then
  echo "BLOCKED: display :99 is already in use by another X server"
  exit 2
fi

Xvfb :99 -screen 0 1920x1080x24 >/tmp/effect-spike-xvfb.log 2>&1 &
XVFB_PID=$!
for _ in $(seq 1 50); do
  DISPLAY=:99 xdotool getdisplaygeometry >/dev/null 2>&1 && break
  sleep 0.1
done
if ! DISPLAY=:99 xdotool getdisplaygeometry >/dev/null 2>&1; then
  echo "FAIL: Xvfb :99 did not start"
  cat /tmp/effect-spike-xvfb.log >&2 || true
  exit 1
fi
export DISPLAY=:99

# Underlying click receiver (stands in for xev).
cat >"$tmp/receiver.py" <<'PY'
import sys
import tkinter

log = open(sys.argv[1], "a", buffering=1)
root = tkinter.Tk()
root.title("Effect Spike Receiver")
root.geometry("500x400+600+250")
root.configure(background="#204020")


def record(event):
    log.write(
        f"ButtonPress button={event.num} root={event.x_root},{event.y_root} time={event.time}\n"
    )
    log.flush()


root.bind("<ButtonPress>", record)
root.mainloop()
PY
python3 "$tmp/receiver.py" "$tmp/receiver.log" >"$tmp/receiver.stdout" 2>&1 &
RECEIVER_PID=$!
receiver_up=""
for _ in $(seq 1 50); do
  if xdotool search --onlyvisible --name "Effect Spike Receiver" >/dev/null 2>&1; then
    receiver_up=1
    break
  fi
  sleep 0.1
done
if [ -z "$receiver_up" ]; then
  echo "FAIL: underlying click receiver did not open"
  cat "$tmp/receiver.stdout" >&2 || true
  exit 1
fi

ready="$tmp/ready"
ASTER_READY_FILE="$ready" dotnet run --project "$spike" -c Release --no-build >/tmp/effect-spike.log 2>&1 &
SPIKE_PID=$!
for _ in $(seq 1 100); do [ -f "$ready" ] && break; sleep 0.1; done
if [ ! -f "$ready" ]; then
  echo "FAIL: spike did not become ready"
  tail -n 40 /tmp/effect-spike.log >&2 || true
  exit 1
fi

effect="$(xdotool search --onlyvisible --name "Effect Spike Effect" | head -1)"
island="$(xdotool search --onlyvisible --name "Effect Spike Island" | head -1)"
if [ -z "$effect" ] || [ -z "$island" ]; then
  echo "FAIL: effect or island window is missing"
  exit 1
fi

geometry() {
  xdotool getwindowgeometry --shell "$1" |
    awk -F= '/^(X|Y|WIDTH|HEIGHT)=/{printf "%s%s", sep, $2; sep=","} END{print ""}' |
    awk -F, '{printf "%s,%s %sx%s\n", $1, $2, $3, $4}'
}
effect_geometry="$(geometry "$effect")"
island_geometry="$(geometry "$island")"
if [ "$effect_geometry" != "580,0 760x460" ]; then
  echo "FAIL: effect geometry is $effect_geometry, expected 580,0 760x460"
  exit 1
fi
if [ "$island_geometry" != "810,0 300x38" ]; then
  echo "FAIL: island geometry is $island_geometry, expected 810,0 300x38"
  exit 1
fi

# Click inside the effect window but outside the island.
xdotool mousemove 960 400 click 1
sleep 0.5
if ! grep -q "ButtonPress" "$tmp/receiver.log"; then
  echo "FAIL: click outside the island did not reach the underlying window"
  echo "Record FAIL in spike/phase4-effect-layer/README.md; use InsetEffectLayer."
  cat "$tmp/receiver.log" >&2 || true
  exit 1
fi

# Click inside the island: the underlying receiver must not get a new press.
before="$(grep -c "ButtonPress" "$tmp/receiver.log" || true)"
xdotool mousemove 960 20 click 1
sleep 0.5
after="$(grep -c "ButtonPress" "$tmp/receiver.log" || true)"
if [ "$before" != "$after" ]; then
  echo "FAIL: island click leaked to the underlying window"
  exit 1
fi

echo "PASS: synthetic X11 gate at 100%. Run the KDE XWayland real-pointer gate manually."
