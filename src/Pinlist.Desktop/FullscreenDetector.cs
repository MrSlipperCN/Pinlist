using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Pinlist.Desktop;

internal static class FullscreenDetector
{
    private const int EdgeTolerance = 2;

    public static bool IsForegroundWindowFullscreen(nint pinlistWindowHandle)
    {
        var foreground = GetForegroundWindow();
        if (foreground == nint.Zero || foreground == pinlistWindowHandle) return false;
        if (IsWindowsShellSurface(foreground)) return false;
        if (IsIconic(foreground) || !GetWindowRect(foreground, out var windowRect)) return false;

        var monitor = Screen.FromHandle(foreground).Bounds;
        return Math.Abs(windowRect.Left - monitor.Left) <= EdgeTolerance
               && Math.Abs(windowRect.Top - monitor.Top) <= EdgeTolerance
               && Math.Abs(windowRect.Right - monitor.Right) <= EdgeTolerance
               && Math.Abs(windowRect.Bottom - monitor.Bottom) <= EdgeTolerance;
    }

    public static bool IsOnSameMonitor(nint firstWindow, nint secondWindow)
    {
        if (firstWindow == nint.Zero || secondWindow == nint.Zero) return false;
        return string.Equals(
            Screen.FromHandle(firstWindow).DeviceName,
            Screen.FromHandle(secondWindow).DeviceName,
            StringComparison.OrdinalIgnoreCase);
    }

    public static nint GetForegroundHandle() => GetForegroundWindow();

    public static bool IsDesktopForegroundWindow()
    {
        var foreground = GetForegroundWindow();
        if (foreground == nint.Zero) return false;

        var className = GetWindowClassName(foreground);
        if (className is "Progman" or "WorkerW" or "SHELLDLL_DefView" or "SysListView32")
            return true;

        var shellWindow = GetShellWindow();
        if (foreground == shellWindow || (shellWindow != nint.Zero && IsChild(shellWindow, foreground)))
            return true;

        // Some Windows 11 builds leave SearchHost's full-monitor CoreWindow
        // in the foreground after Show Desktop, even though no search UI is open.
        return className == "Windows.UI.Core.CoreWindow"
               && IsProcessNamed(foreground, "SearchHost", "SearchApp")
               && CoversItsMonitor(foreground);
    }

    private static bool IsWindowsShellSurface(nint window)
    {
        var className = GetWindowClassName(window);
        if (className is "Progman"
            or "WorkerW"
            or "SHELLDLL_DefView"
            or "SysListView32"
            or "Shell_TrayWnd"
            or "Shell_SecondaryTrayWnd")
        {
            return true;
        }

        // On recent Windows 11 builds, Show Desktop can leave one of these
        // shell processes' full-monitor CoreWindow surfaces in the foreground.
        // They are desktop/search UI, not fullscreen applications.
        return IsProcessNamed(window,
            "SearchHost", "SearchApp", "ShellExperienceHost", "StartMenuExperienceHost");
    }

    private static bool CoversItsMonitor(nint window)
    {
        if (!GetWindowRect(window, out var windowRect)) return false;
        var monitor = Screen.FromHandle(window).Bounds;
        return Math.Abs(windowRect.Left - monitor.Left) <= EdgeTolerance
               && Math.Abs(windowRect.Top - monitor.Top) <= EdgeTolerance
               && Math.Abs(windowRect.Right - monitor.Right) <= EdgeTolerance
               && Math.Abs(windowRect.Bottom - monitor.Bottom) <= EdgeTolerance;
    }

    private static bool IsProcessNamed(nint window, params string[] expectedNames)
    {
        GetWindowThreadProcessId(window, out var processId);
        if (processId == 0) return false;

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return expectedNames.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string GetWindowClassName(nint window)
    {
        var className = new StringBuilder(256);
        return GetClassName(window, className, className.Capacity) > 0
            ? className.ToString()
            : string.Empty;
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern nint GetShellWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsChild(nint parent, nint child);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hWnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out NativeRect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
