#!/usr/bin/env bash
# Umbrella verifier for Phase 4A.
#
# Every gate is recorded as PASS, PARTIAL, BLOCKED, or FAIL. PARTIAL/BLOCKED
# gates do not fail the run, but they are printed and must be carried into
# docs/results/phase4a-report.md. A genuine failure exits nonzero.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
exe="${1:-$root/src/Aster.Avalonia/Aster.Desktop.Linux/bin/Release/net10.0/Aster.Avalonia}"

gate_names=()
gate_status=()

add_gate() {
    gate_names+=("$1")
    gate_status+=("$2")
    printf '==> %-18s %s\n' "$1" "$2"
}

# 0 = PASS, 2 = BLOCKED, 3 = PARTIAL, anything else = FAIL.
run_probe() {
    local name="$1"
    shift
    local rc=0
    "$@" || rc=$?
    case "$rc" in
        0) add_gate "$name" PASS ;;
        2) add_gate "$name" BLOCKED ;;
        3) add_gate "$name" PARTIAL ;;
        *) add_gate "$name" FAIL ;;
    esac
}

echo "== Build =="
if ! dotnet build "$root/src/Aster.Avalonia/Aster.Avalonia.sln" \
    -c Release -p:EnableWindowsTargeting=true; then
    echo "FAIL: build" >&2
    exit 1
fi
add_gate "build" PASS

echo "== Unit tests =="
for project in \
    "$root/src/Aster.Avalonia/Aster.Core.Tests/Aster.Core.Tests.csproj" \
    "$root/src/Aster.Avalonia/Aster.UI.Tests/Aster.UI.Tests.csproj" \
    "$root/src/Aster.Avalonia/Aster.Platform.Linux.Tests/Aster.Platform.Linux.Tests.csproj"; do
    if ! dotnet test "$project" -c Release; then
        echo "FAIL: $project" >&2
        exit 1
    fi
done
add_gate "core/ui/linux tests" PASS

echo "== Runtime gates =="

# The synthetic effect-layer gate is independently allowed to fall back to the
# inset effect layer. Record that as PARTIAL, never PASS.
effect_rc=0
"$root/scripts/test-effect-layer.sh" || effect_rc=$?
case "$effect_rc" in
    0)
        add_gate "effect layer" PASS
        ;;
    2)
        add_gate "effect layer" BLOCKED
        ;;
    *)
        add_gate "effect layer" FAIL
        echo "FAIL: effect layer gate failed; the shipped X11 head has no inset fallback"
        ;;
esac

run_probe "window shell" "$root/scripts/test-window-shell.sh" "$exe"
run_probe "linux settings" "$root/scripts/test-linux-settings.sh" "$exe"
run_probe "linux tray" "$root/scripts/test-linux-tray.sh" "$exe"
run_probe "linux AOT" "$root/scripts/test-aot-linux.sh"

echo
echo "== Phase 4A gate summary =="
failed=0
for i in "${!gate_names[@]}"; do
    printf '%-18s %s\n' "${gate_names[$i]}" "${gate_status[$i]}"
    if [ "${gate_status[$i]}" = "FAIL" ]; then
        failed=1
    fi
done

if [ "$failed" -ne 0 ]; then
    echo "Phase 4A has FAIL gates." >&2
    exit 1
fi

echo
echo "4A checks complete. Review docs/results/phase4a-report.md for PASS/PARTIAL/BLOCKED."
