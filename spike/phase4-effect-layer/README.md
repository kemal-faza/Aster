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

Additional checks from the same run:

- The committed script now asserts the island and effect windows and their
  geometry: island `810,0 300x38`; effect `580,0 760x460`; receiver
  `600,250 500x400`.
- X11 shape (independent): effect **input shape = 0 rectangles** (bounding
  shape = 1); island input shape = 1. Click-through is therefore caused by the
  emptied input shape, not by an absent window.
- Negative control (independent): the same spike built with
  `XShapeCombineRectangles(..., ShapeInput, ...)` removed no longer delivers
  the `960,400` click to the receiver (the effect window swallows it), so the
  PASS is not vacuous.

## KDE XWayland real-pointer gate (100/125/150/200%)

**PASS.** `scripts/test-effect-layer-kde.sh` ran the spike on the real KDE
Wayland session (`DISPLAY=:0`, XWayland) with a real pointer from a one-shot
`/dev/uinput` device (`spike/phase4-effect-layer/uinput_click.c`, run through
`sudo`; the agent used one secret card instead of repeated pkexec prompts).
`xdotool` XTest cannot drive this gate: KWin ignores XWayland pointer warps and
XTest motion, which is why the earlier attempt was recorded as blocked. The
uinput device produces kernel-level motion/buttons, so KWin routes them like a
physical mouse. The helper warms up for 2 s and settles 800 ms before each
click; an early 125% run was flaky before that timing fix and passed on rerun.

The test used a managed receiver for the through-click/focus check and a
separate override-redirect strip under the island for leak detection. Stacking
was `receiver -> effect -> island` (`_NET_CLIENT_LIST_STACKING`), the effect and
island were raised above both receivers, and the island receiver was lowered
below them. Avalonia scale was varied with `Xft.dpi` on the same KWin session
(96/120/144/192 = 1.0/1.25/1.5/2.0); the KWin compositor itself stayed at its
native scale.

| Scale | Effect geometry | Island geometry | Through click | Island click | Result |
|---|---|---|---|---|---|
| 100% | `2180,80 760x460` | `2410,80 300x38` | `2380,380` | `2560,114` | PASS |
| 125% | `2085,80 950x575` | `2372,80 375x47` | `2285,380` | `2559,123` | PASS |
| 150% | `1990,80 1140x690` | `2335,80 450x57` | `2190,380` | `2560,133` | PASS |
| 200% | `1800,80 1520x920` | `2260,80 600x76` | `2000,380` | `2560,152` | PASS |

For every scale the managed receiver logged exactly one `ButtonPress` at the
through point, the island receiver logged **zero**, `_NET_ACTIVE_WINDOW`
remained the managed receiver after both clicks, and the pointer ended on the
island window. A separate 200% run also recorded `RSS=143172 kB` and
`CPU=2.6%` for the spike process after 42 s; flicker was not instrumented.
Both the through click and the island click were real kernel input, not XTest.

## Decision

**PASS -> `X11EffectLayer` in Part 2.** The synthetic Xvfb gate and the real
KDE XWayland gate both pass. The scale matrix was exercised through Avalonia's
Xft scale on the real compositor; if a strict KDE output-scale change is
required later, re-run the same script after changing the Plasma scale. Part 2
may use `X11EffectLayer` rather than `InsetEffectLayer`.

## Notes / limitations

- The Xvfb synthetic gate has no window manager, so focus hand-off and
  compositor behavior are covered only by the KDE gate.
- The KDE gate measured click-through, island leak, focus, stacking, RSS, and
  CPU. Flicker was not instrumented, and multi-monitor placement was not
  exercised beyond the primary output used by the spike.
- The empty input shape is applied after `Opened`; a compositor may repaint once
  before the shape lands. The KDE gate did not instrument the first frame.
- Scaling arithmetic assumes `Screens.Primary`. Xvfb reports scaling 1.0; the
  KDE matrix varied Avalonia scaling through `Xft.dpi`, not KWin's output scale.
- The uinput helper is a short-lived root process launched through `sudo`; it
  creates a device, clicks the requested points, and destroys the device. It is
  not a daemon and leaves no `/dev/uinput` access behind. The gate script asks
  for one sudo authentication and reuses the cached timestamp for every scale.
