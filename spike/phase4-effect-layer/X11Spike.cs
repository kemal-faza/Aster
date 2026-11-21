using System;
using System.Runtime.InteropServices;

namespace EffectLayerSpike;

internal static class X11Spike
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
    [DllImport(X11)] private static extern IntPtr XInternAtom(IntPtr display, string name, bool onlyIfExists);
    [DllImport(X11)]
    private static extern int XChangeProperty(
        IntPtr display, IntPtr window, IntPtr property, IntPtr type,
        int format, int mode, IntPtr[] data, int count);
    [DllImport(X11)]
    private static extern int XSetWMHints(IntPtr display, IntPtr window, ref XWMHints hints);
    [DllImport(XExt)]
    private static extern int XShapeCombineRectangles(
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

    public static void Apply(IntPtr island, IntPtr effect)
    {
        if (island == IntPtr.Zero || effect == IntPtr.Zero)
        {
            return;
        }

        var display = XOpenDisplay(null);
        if (display == IntPtr.Zero)
        {
            return;
        }

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

        // Empty input shape on the effect window: it must never receive a click.
        XShapeCombineRectangles(display, effect, ShapeInput, 0, 0,
            IntPtr.Zero, 0, ShapeSet, Unsorted);

        // Island on top, effect directly below it.
        XRestackWindows(display, new[] { island, effect }, 2);
        XFlush(display);
    }
}
