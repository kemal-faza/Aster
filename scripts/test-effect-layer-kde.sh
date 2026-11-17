#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
spike="$root/spike/phase4-effect-layer/EffectLayerSpike.csproj"
out="${ASTER_KDE_GATE_OUT:-/tmp/opencode/aster-kde-gate}"
rm -rf "$out"; mkdir -p "$out"
exec > >(tee "$out/gate.log") 2>&1

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export DISPLAY=:0
xrdb -query > "$out/xrdb-backup.txt"

command -v gcc sudo python3 xdotool xprop xrdb >/dev/null || {
  echo "BLOCKED: missing gcc, sudo, python3, xdotool, xprop, or xrdb"
  exit 2
}
python3 -c "import tkinter" >/dev/null 2>&1 || {
  echo "BLOCKED: python3-tkinter is missing"
  exit 2
}

dotnet build "$spike" -c Release >"$out/build.log" 2>&1
gcc -O2 -Wall -Wextra -o "$out/aster-uinput-click" \
  "$root/spike/phase4-effect-layer/uinput_click.c"
echo "BUILD_OK"
echo "WARNING: this gate moves the real pointer and clicks through sudo."
if ! sudo -n true 2>/dev/null; then
  echo "One sudo authentication is required for the uinput virtual pointer."
  sudo -v
fi

cat >"$out/normal_receiver.py" <<'PY'
import sys
import tkinter
log = open(sys.argv[1], "w", buffering=1)
root = tkinter.Tk()
root.title("Aster Gate Receiver")
root.geometry("5120x1400+0+0")
root.configure(background="#1d4d2d")
tkinter.Label(root, text="Aster gate normal receiver", font=("Sans", 28, "bold"),
              fg="#d8ffe4", bg="#1d4d2d").pack(pady=80)
def record(event):
    log.write(f"ButtonPress root={event.x_root},{event.y_root} time={event.time}\n")
    log.flush()
root.bind("<ButtonPress>", record)
root.mainloop()
PY

cat >"$out/island_receiver.py" <<'PY'
import sys
import tkinter
log = open(sys.argv[1], "w", buffering=1)
root = tkinter.Tk()
root.title("Aster Gate Island Receiver")
root.overrideredirect(True)
root.geometry("1200x200+2000+0")
root.configure(background="#4d1d1d")
def record(event):
    log.write(f"ButtonPress root={event.x_root},{event.y_root} time={event.time}\n")
    log.flush()
root.bind("<ButtonPress>", record)
root.mainloop()
PY

cleanup() {
  set +e
  kill ${SPIKE_PID:-} ${NORMAL_PID:-} ${ISLAND_PID:-} 2>/dev/null
  wait ${SPIKE_PID:-} ${NORMAL_PID:-} ${ISLAND_PID:-} 2>/dev/null
  xrdb -load "$out/xrdb-backup.txt"
}
trap cleanup EXIT

passes=0
fails=0
scales="${ASTER_KDE_GATE_SCALES:-96:100 120:125 144:150 192:200}"
for entry in $scales; do
  dpi=${entry%%:*}
  scale=${entry##*:}
  echo "===== SCALE ${scale}% (Xft.dpi=${dpi}) ====="
  printf 'Xft.dpi:\t%s\n' "$dpi" > "$out/xft-$dpi"
  xrdb -merge "$out/xft-$dpi"

  rm -f "$out/normal-$scale.log" "$out/island-$scale.log"
  python3 "$out/normal_receiver.py" "$out/normal-$scale.log" >"$out/normal-$scale.stdout" 2>&1 &
  NORMAL_PID=$!
  python3 "$out/island_receiver.py" "$out/island-$scale.log" >"$out/island-$scale.stdout" 2>&1 &
  ISLAND_PID=$!
  for _ in $(seq 1 50); do
    xdotool search --onlyvisible --name "Aster Gate Receiver" >/dev/null 2>&1 &&
      xdotool search --onlyvisible --name "Aster Gate Island Receiver" >/dev/null 2>&1 && break
    sleep 0.1
  done
  normal=$(xdotool search --onlyvisible --name "Aster Gate Receiver" | head -1)
  island_receiver=$(xdotool search --onlyvisible --name "Aster Gate Island Receiver" | head -1)
  echo "NORMAL_ID=$normal ISLAND_RECEIVER_ID=$island_receiver"

  ready="$out/ready-$scale"
  rm -f "$ready"
  env -u WAYLAND_DISPLAY ASTER_READY_FILE="$ready" \
    dotnet run --project "$spike" -c Release --no-build >"$out/spike-$scale.log" 2>&1 &
  SPIKE_PID=$!
  for _ in $(seq 1 100); do [ -f "$ready" ] && break; sleep 0.1; done
  if [ ! -f "$ready" ]; then
    echo "FAIL ${scale}: spike did not become ready"; fails=$((fails+1)); continue
  fi

  island=$(xdotool search --onlyvisible --name "Effect Spike Island" | head -1)
  effect=$(xdotool search --onlyvisible --name "Effect Spike Effect" | head -1)
  xdotool getwindowgeometry --shell "$effect" >"$out/effect-$scale.geometry"
  xdotool getwindowgeometry --shell "$island" >"$out/island-$scale.geometry"
  echo "--- effect"; cat "$out/effect-$scale.geometry"
  echo "--- island"; cat "$out/island-$scale.geometry"

  xdotool windowraise "$effect"
  sleep 0.2
  xdotool windowraise "$island"
  sleep 0.2
  xdotool windowlower "$island_receiver"
  sleep 0.4
  echo "--- stacking (bottom to top)"; xprop -root _NET_CLIENT_LIST_STACKING
  echo "--- active before"; xprop -root _NET_ACTIVE_WINDOW

  ex=$(awk -F= '/^X=/{print $2}' "$out/effect-$scale.geometry")
  ey=$(awk -F= '/^Y=/{print $2}' "$out/effect-$scale.geometry")
  ix=$(awk -F= '/^X=/{print $2}' "$out/island-$scale.geometry")
  iy=$(awk -F= '/^Y=/{print $2}' "$out/island-$scale.geometry")
  iw=$(awk -F= '/^WIDTH=/{print $2}' "$out/island-$scale.geometry")
  ih=$(awk -F= '/^HEIGHT=/{print $2}' "$out/island-$scale.geometry")
  through_x=$((ex + 200)); through_y=$((ey + 300))
  island_x=$((ix + iw / 2)); island_y=$((iy + ih - 4))
  echo "POINTS through=$through_x,$through_y island=$island_x,$island_y"

  echo "--- sudo clicks for scale ${scale}%"
  if ! sudo -n "$out/aster-uinput-click" "$through_x" "$through_y" "$island_x" "$island_y"; then
    echo "FAIL ${scale}: sudo/helper failed"; fails=$((fails+1)); continue
  fi
  sleep 0.8

  echo "--- normal log"; cat "$out/normal-$scale.log" || true
  echo "--- island receiver log"; cat "$out/island-$scale.log" || true
  echo "--- active after"; xprop -root _NET_ACTIVE_WINDOW
  echo "--- pointer after"; xdotool getmouselocation --shell

  normal_presses=$(grep -c ButtonPress "$out/normal-$scale.log" || true)
  island_presses=$(grep -c ButtonPress "$out/island-$scale.log" || true)
  through_hits=$(grep -c "root=$through_x,$through_y" "$out/normal-$scale.log" || true)
  active_after=$(xprop -root _NET_ACTIVE_WINDOW)
  normal_hex=$(printf '0x%x' "$normal")
  echo "RESULT scale=${scale} normal_presses=$normal_presses through_hits=$through_hits island_presses=$island_presses active=$active_after normal_hex=$normal_hex"

  ok=1
  [ "$through_hits" -ge 1 ] || ok=0
  [ "$island_presses" -eq 0 ] || ok=0
  grep -qi "$normal_hex" <<<"$active_after" || ok=0
  if [ "$ok" -eq 1 ]; then
    echo "PASS ${scale}%"
    passes=$((passes+1))
  else
    echo "FAIL ${scale}%"
    fails=$((fails+1))
  fi

  kill "$SPIKE_PID" "$NORMAL_PID" "$ISLAND_PID" 2>/dev/null || true
  wait "$SPIKE_PID" "$NORMAL_PID" "$ISLAND_PID" 2>/dev/null || true
  unset SPIKE_PID NORMAL_PID ISLAND_PID
  sleep 0.3
done

xrdb -load "$out/xrdb-backup.txt"
echo "===== SUMMARY passes=$passes fails=$fails ====="
[ "$fails" -eq 0 ]
