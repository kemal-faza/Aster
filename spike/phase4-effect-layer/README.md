# Effect Layer Spike

A1 candidate: Avalonia 12.1.6 transparent, borderless, topmost `Window`s plus an
X11 input shape applied to the effect window.

- Island: 300x38 at `810,0` (100% scaling), opaque #F2070708, input enabled.
- Effect: 760x460 at `580,0` (100% scaling), transparent to pixels and clicks,
  with an empty X11 **input** shape and a non-empty bounding shape.
- Stacking: receiver (bottom) -> effect -> island (top), via `XRestackWindows`.
- WM hints: `_NET_WM_WINDOW_TYPE_DOCK`, `_NET_WM_STATE_ABOVE`,
  `_NET_WM_STATE_SKIP_TASKBAR`, `_NET_WM_STATE_SKIP_PAGER`, `WM_HINTS.input = 0`.

## Xvfb synthetic gate (100%)

`scripts/test-effect-layer.sh`, Fedora Linux, Xvfb `:99` at 1920x1080x24,
.NET SDK 10.0.404, Avalonia 12.1.6:

```
$ scripts/test-effect-layer.sh
PASS: synthetic X11 gate at 100%. Run the KDE XWayland real-pointer gate manually.
$ echo $?
0
```

The gate clicks at `960,400` (inside the effect, outside the island) and requires
the underlying click receiver to log a `ButtonPress`; it then clicks `960,20`
(inside the island) and requires **no new** `ButtonPress`; it also requires the
effect window to exist.

Underlying-window client: a **python3/tkinter** window at `500x400+600+250` that
binds `<ButtonPress>` and appends each press to a log, because `xev` is **not
installed** on this Fedora host and no system packages may be installed for this
spike. It occupies the same screen region the plan's `xev` used, so the click
coordinates are unchanged.

Independent checks from the same run (not part of the committed script):

- Window geometry: island `810,0 300x38`; effect `580,0 760x460`; receiver
  `600,250 500x400` — both click points land inside the intended windows.
- X11 shape: effect **input shape = 0 rectangles** (bounding shape = 1);
  island input shape = 1. Click-through is therefore caused by the emptied
  input shape, not by an absent window.
- Negative control: the same spike built with `XShapeCombineRectangles(...,
  ShapeInput, ...)` removed no longer delivers the `960,400` click to the
  receiver (the effect window swallows it), so the PASS is not vacuous.

## KDE XWayland real-pointer gate (100/125/150/200%)

**BLOCKED for automation; not a FAIL.** The controller ran the spike on the
real KDE Wayland session (`DISPLAY=:0`, XWayland) at the session's native 200%
scale. The spike mapped correctly: effect `1520x920` at `1800,80`, island
`600x76` at `2260,80`, and `_NET_CLIENT_LIST_STACKING` showed receiver ->
effect -> island (effect above the receiver). The shape call therefore ran
under KWin.

The pointer gate itself could not be driven from this session: KWin does not
honor `XWarpPointer`/XTest motion from an XWayland client (`xdotool mousemove`
leaves the pointer at `5120,900`), and creating a kernel virtual pointer would
need write access to `/dev/uinput` (root/input group; sudo password required).
No real pointer was moved and no click was claimed.

Procedure for the manual gate, at each scale: open a known window under the
pill, click the pill's outside/inside regions, confirm the underlying window
activates and the effect never takes focus; note flicker and RSS/CPU. At 200%
the tested geometry above is the reference.

## Decision

**BLOCKED -> `InsetEffectLayer` in Part 2 until a manual KDE gate records
PASS.** The synthetic Xvfb gate PASSes with direct shape and negative-control
evidence, so A1 remains the primary candidate, but Part 2 must not adopt
`X11EffectLayer` without the real-pointer result. This follows the plan's
conservative rule: no PASS, no X11 effect layer.

## Notes / limitations

- Xvfb has no window manager, so focus hand-off, `_NET_WM_STATE_ABOVE`
  enforcement, and compositor behavior were **not** exercised here; only the
  X11 input shape and stacking were.
- Flicker, RSS, CPU, and multi-monitor placement are unmeasured; they belong to
  the KDE gate.
- The empty input shape is applied after `Opened`; a compositor may repaint once
  before the shape lands. The KDE gate should watch for the first-frame flicker.
- Scaling arithmetic assumes `Screens.Primary`; Xvfb reports scaling 1.0.
