#!/usr/bin/env bash
# Linux audio-spectrum integration gate.
#
# Plays a generated 440 Hz tone through a temporary null sink, reads the real
# ParecSpectrumSource through tools/Aster.SpectrumProbe, and checks that the
# mid band rises while the tone plays and falls afterward.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

missing=()
for tool in pactl parec paplay python3 dotnet; do
    command -v "$tool" >/dev/null 2>&1 || missing+=("$tool")
done
if [ "${#missing[@]}" -gt 0 ]; then
    echo "BLOCKED: missing dependency: ${missing[*]}"
    exit 2
fi

work="$(mktemp -d "${TMPDIR:-/tmp}/aster-spectrum.XXXXXX")"
original_sink="$(pactl get-default-sink)"
module=""
probe_pid=""

cleanup() {
    if [ -n "${probe_pid:-}" ]; then
        kill "$probe_pid" 2>/dev/null || true
        wait "$probe_pid" 2>/dev/null || true
    fi
    if [ -n "$module" ]; then
        pactl unload-module "$module" >/dev/null 2>&1 || true
    fi
    if [ -n "$original_sink" ]; then
        pactl set-default-sink "$original_sink" >/dev/null 2>&1 || true
    fi
    rm -rf "$work"
}
trap cleanup EXIT

module="$(pactl load-module module-null-sink sink_name=aster_spectrum_test)"
pactl set-default-sink aster_spectrum_test >/dev/null

python3 - "$work/tone.wav" <<'PY'
import math
import struct
import sys
import wave

rate = 48000
seconds = 2
amplitude = 0.5

with wave.open(sys.argv[1], "wb") as handle:
    handle.setnchannels(1)
    handle.setsampwidth(2)
    handle.setframerate(rate)
    frames = bytearray()
    for i in range(rate * seconds):
        value = int(amplitude * 32767 * math.sin(2 * math.pi * 440 * i / rate))
        frames += struct.pack("<h", value)
    handle.writeframes(frames)
PY

project="$root/src/Aster.Avalonia/tools/Aster.SpectrumProbe/Aster.SpectrumProbe.csproj"
dotnet build "$project" -c Release --nologo >"$work/probe-build.log" 2>&1
probe_dll="$root/src/Aster.Avalonia/tools/Aster.SpectrumProbe/bin/Release/net10.0/Aster.SpectrumProbe.dll"

dotnet "$probe_dll" --seconds 6 --csv "$work/probe.csv" >"$work/probe.log" 2>&1 &
probe_pid=$!

# Build first and start the tone immediately. The 6 s probe window covers the
# 2 s tone even if `parec` takes a few hundred milliseconds to connect; the
# null sink is activated by the tone itself.
sleep 0.2
paplay --device=aster_spectrum_test "$work/tone.wav" &
tone_pid=$!

probe_rc=0
wait "$probe_pid" || probe_rc=$?
probe_pid=""
wait "$tone_pid" 2>/dev/null || true

if [ "$probe_rc" -ne 0 ]; then
    echo "FAIL: spectrum probe exited $probe_rc" >&2
    cat "$work/probe.log" >&2 || true
    exit 1
fi

python3 - "$work/probe.csv" <<'PY'
import csv
import sys

rows = []
with open(sys.argv[1], newline="") as handle:
    for row in csv.DictReader(handle):
        rows.append({
            "t": int(row["t_ms"]),
            "available": row["available"] == "1",
            "bass": float(row["bass"]),
            "mid": float(row["mid"]),
        })

available = [row for row in rows if row["available"]]
if not available:
    raise SystemExit("FAIL: spectrum source never produced a frame")

mid = max(row["mid"] for row in available)
bass = max(row["bass"] for row in available)
last_t = rows[-1]["t"]
tail = [row["mid"] for row in rows if row["t"] > last_t - 1000]

if mid < 0.25:
    raise SystemExit(f"FAIL: mid band stayed low ({mid:.3f})")
if mid <= bass:
    raise SystemExit(f"FAIL: mid ({mid:.3f}) did not exceed bass ({bass:.3f})")
if any(value > 0.2 for value in tail):
    raise SystemExit(f"FAIL: mid did not fall after the tone: {tail}")

print(f"Audio spectrum PASS: mid={mid:.3f} bass={bass:.3f} tail={tail}")
PY
