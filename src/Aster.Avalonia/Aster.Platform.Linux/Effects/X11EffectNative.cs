using System.Runtime.InteropServices;

namespace Aster.Platform.Linux.Effects;

internal static class X11EffectNative
{
    private const string X11 = "libX11";
    private const string XExt = "libXext";
    private const int XaAtom = 4;
    private const int PropModeReplace = 0;
    private const int InputHint = 1;
    private const int ShapeInput = 2;
    private const int ShapeSet = 0;
    private const int Unsorted = 0;

    [DllImport(X11)] private static extern IntPtr XOpenDisplay(string? name);
    [DllImport(X11)] private static extern int XCloseDisplay(IntPtr display);
    [DllImport(X11)]
    private static extern IntPtr XInternAtom(IntPtr display, string name, bool onlyIfExists);
    [DllImport(X11)]
    private static extern int XChangeProperty(
        IntPtr display, IntPtr window, IntPtr property, IntPtr type,
        int format, int mode, IntPtr[] data, int count);
    [DllImport(X11)]
    private static extern int XSetWMHints(IntPtr display, IntPtr window, ref XWMHints hints);
    [DllImport(X11)]
    private static extern int XSetTransientForHint(IntPtr display, IntPtr window, IntPtr propWindow);
    [DllImport(XExt)]
    private static extern void XShapeCombineRectangles(
        IntPtr display, IntPtr window, int destKind, int xOff, int yOff,
        IntPtr rectangles, int count, int operation, int ordering);
    [DllImport(X11)]
    private static extern int XRestackWindows(IntPtr display, IntPtr[] windows, int count);
    [DllImport(X11)] private static extern int XFlush(IntPtr display);

    [StructLayout(LayoutKind.Sequential)]
    private struct XWMHints
    {
        public long flags;
        public int input;
        public int initialState;
        public IntPtr iconPixmap;
        public IntPtr iconWindow;
        public int iconX;
        public int iconY;
        public IntPtr iconMask;
        public IntPtr windowGroup;
    }

    /// <summary>
    /// Makes both windows dock-type, input-transparent, above and skip-taskbar,
    /// empties the effect window input shape, then stacks the island above the
    /// effect. Returns false when the display or a window handle is unavailable.
    /// </summary>
    public static bool Apply(IntPtr island, IntPtr effect)
    {
        if (island == IntPtr.Zero || effect == IntPtr.Zero)
        {
            return false;
        }

        var display = XOpenDisplay(null);
        if (display == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var stateAtom = XInternAtom(display, "_NET_WM_STATE", false);
            var atoms = new[]
            {
                XInternAtom(display, "_NET_WM_STATE_ABOVE", false),
                XInternAtom(display, "_NET_WM_STATE_SKIP_TASKBAR", false),
                XInternAtom(display, "_NET_WM_STATE_SKIP_PAGER", false)
            };
            var typeAtom = XInternAtom(display, "_NET_WM_WINDOW_TYPE", false);
            var dockAtom = XInternAtom(display, "_NET_WM_WINDOW_TYPE_DOCK", false);

            foreach (var handle in new[] { island, effect })
            {
                var hints = new XWMHints { flags = InputHint, input = 0 };
                XSetWMHints(display, handle, ref hints);
                XChangeProperty(display, handle, stateAtom, new IntPtr(XaAtom), 32,
                    PropModeReplace, atoms, atoms.Length);
                XChangeProperty(display, handle, typeAtom, new IntPtr(XaAtom), 32,
                    PropModeReplace, new[] { dockAtom }, 1);
            }

            // The effect is logically owned by the island, but KWin forces an
            // X11 transient above its owner. Clearing WM_TRANSIENT_FOR lets the
            // island stay on top of the glow, as validated by the A1 spike.
            XSetTransientForHint(display, effect, IntPtr.Zero);

            // Empty input shape on the effect window: it must never receive a
            // click, so clicks fall through to the window below.
            XShapeCombineRectangles(display, effect, ShapeInput, 0, 0,
                IntPtr.Zero, 0, ShapeSet, Unsorted);

            // Island on top, effect directly below it.
            XRestackWindows(display, new[] { island, effect }, 2);
            XFlush(display);
            return true;
        }
        finally
        {
            XCloseDisplay(display);
        }
    }
}
