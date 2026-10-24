using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Pinlist.Desktop;

internal static class BorderlessResizeHelper
{
    private const int WmNcHitTest = 0x0084;

    public static void Enable(Window window, int edgeThickness = 8)
    {
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            HwndSource.FromHwnd(handle)?.AddHook((nint hwnd, int message, nint wParam, nint lParam, ref bool handled) =>
            {
                if (message != WmNcHitTest || window.ResizeMode == ResizeMode.NoResize
                    || window.WindowState != WindowState.Normal || !GetWindowRect(hwnd, out var rect))
                {
                    return nint.Zero;
                }

                var packed = unchecked((long)lParam);
                var x = unchecked((short)(packed & 0xFFFF));
                var y = unchecked((short)((packed >> 16) & 0xFFFF));
                var left = x <= rect.Left + edgeThickness;
                var right = x >= rect.Right - edgeThickness;
                var top = y <= rect.Top + edgeThickness;
                var bottom = y >= rect.Bottom - edgeThickness;
                var hit = top && left ? 13 : top && right ? 14 : bottom && left ? 16 : bottom && right ? 17
                    : left ? 10 : right ? 11 : top ? 12 : bottom ? 15 : 0;
                if (hit == 0) return nint.Zero;

                handled = true;
                return hit;
            });
        };
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hwnd, out WindowRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
