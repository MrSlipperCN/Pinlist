using System.Runtime.InteropServices;
using System.Text;

namespace Pinlist.Desktop;

internal sealed class DesktopWindowHost
{
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const int GwlHwndParent = -8;
    private const long WsChild = 0x40000000L;
    private const long WsVisible = 0x10000000L;
    private const long WsClipChildren = 0x02000000L;
    private const long WsClipSiblings = 0x04000000L;
    private const long WsPopup = unchecked((long)0x80000000L);
    private const long WsExAppWindow = 0x00040000L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExLayered = 0x00080000L;
    private const uint LwaAlpha = 0x00000002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;
    private const int SwShowNoActivate = 4;
    private static readonly nint HwndTop = nint.Zero;
    private static readonly nint HwndTopmost = new(-1);
    private static readonly nint HwndNoTopmost = new(-2);

    private readonly nint _windowHandle;
    private nint _desktopHostHandle;
    private nint _bridgeHandle;
    private byte _opacity = 255;

    public DesktopWindowHost(nint windowHandle) => _windowHandle = windowHandle;

    public bool IsAttached => _bridgeHandle != nint.Zero
                              && IsWindow(_bridgeHandle)
                              && GetParent(_bridgeHandle) == _desktopHostHandle
                              && GetParent(_windowHandle) == _bridgeHandle
                              && IsUsableDesktopHost(_desktopHostHandle);

    public bool Attach()
    {
        if (IsAttached) return true;
        if (_windowHandle == nint.Zero || !GetWindowRect(_windowHandle, out var bounds)) return false;

        CleanupBridgePreservingWindow();
        var desktopHost = FindDesktopHost(out var strategy);
        if (desktopHost == nint.Zero)
        {
            AppLog.Write("Desktop host discovery failed; using a normal non-topmost window.");
            return false;
        }

        var parentOrigin = new NativePoint();
        _ = ClientToScreen(desktopHost, ref parentOrigin);
        var width = Math.Max(1, bounds.Right - bounds.Left);
        var height = Math.Max(1, bounds.Bottom - bounds.Top);
        _bridgeHandle = CreateWindowEx(
            (uint)(WsExToolWindow | WsExLayered), "STATIC", "PinlistDesktopHost",
            (uint)(WsPopup | WsVisible | WsClipChildren | WsClipSiblings),
            bounds.Left, bounds.Top, width, height,
            nint.Zero, nint.Zero, nint.Zero, nint.Zero);
        if (_bridgeHandle == nint.Zero)
        {
            AppLog.Write($"Desktop bridge creation failed. error={Marshal.GetLastPInvokeError()}.");
            return false;
        }

        var bridgeStyle = GetWindowLongPtr(_bridgeHandle, GwlStyle).ToInt64();
        bridgeStyle = (bridgeStyle | WsChild) & ~WsPopup;
        _ = SetWindowLongPtr(_bridgeHandle, GwlStyle, new nint(bridgeStyle));
        _ = SetParent(_bridgeHandle, desktopHost);
        if (GetParent(_bridgeHandle) != desktopHost)
        {
            AppLog.Write($"Desktop bridge attach failed. error={Marshal.GetLastPInvokeError()}.");
            _ = DestroyWindow(_bridgeHandle);
            _bridgeHandle = nint.Zero;
            return false;
        }

        SetChildStyles();
        _ = SetParent(_windowHandle, _bridgeHandle);
        if (GetParent(_windowHandle) != _bridgeHandle)
        {
            AppLog.Write($"WPF bridge attach failed. error={Marshal.GetLastPInvokeError()}.");
            CleanupBridgePreservingWindow();
            return false;
        }

        _desktopHostHandle = desktopHost;
        _ = SetWindowPos(_windowHandle, HwndTop, 0, 0, width, height,
            SwpNoActivate | SwpFrameChanged | SwpShowWindow);
        _ = SetWindowPos(_bridgeHandle, HwndTop,
            bounds.Left - parentOrigin.X, bounds.Top - parentOrigin.Y, width, height,
            SwpNoActivate | SwpShowWindow);
        _ = SetLayeredWindowAttributes(_bridgeHandle, 0, _opacity, LwaAlpha);
        _ = ShowWindow(_windowHandle, SwShowNoActivate);
        _ = ShowWindow(_bridgeHandle, SwShowNoActivate);
        AppLog.Write($"Attached through same-process bridge 0x{_bridgeHandle:X} to desktop host 0x{desktopHost:X} via {strategy}.");
        return true;
    }

    public void EnsureAttached()
    {
        if (!IsAttached) Attach();
    }

    public void Invalidate() => _desktopHostHandle = nint.Zero;

    public bool TryGetScreenBounds(out ScreenBounds bounds)
    {
        var target = IsAttached ? _bridgeHandle : _windowHandle;
        if (GetWindowRect(target, out var nativeBounds))
        {
            bounds = new ScreenBounds(nativeBounds.Left, nativeBounds.Top, nativeBounds.Right, nativeBounds.Bottom);
            return true;
        }
        bounds = default;
        return false;
    }

    public void MoveToScreen(int screenX, int screenY)
    {
        if (!IsAttached) return;
        var parentOrigin = new NativePoint();
        _ = ClientToScreen(_desktopHostHandle, ref parentOrigin);
        _ = SetWindowPos(_bridgeHandle, HwndTop,
            screenX - parentOrigin.X, screenY - parentOrigin.Y, 0, 0, SwpNoActivate | SwpNoSize);
    }

    public void SetOpacity(double opacity)
    {
        _opacity = (byte)Math.Clamp((int)Math.Round(opacity * 255), 0, 255);
        if (_bridgeHandle != nint.Zero && IsWindow(_bridgeHandle))
            _ = SetLayeredWindowAttributes(_bridgeHandle, 0, _opacity, LwaAlpha);
    }

    public void Detach(bool topmost)
    {
        if (_windowHandle == nint.Zero) return;
        var target = IsAttached ? _bridgeHandle : _windowHandle;
        if (!GetWindowRect(target, out var bounds)) return;

        _ = SetParent(_windowHandle, nint.Zero);
        _ = SetWindowLongPtr(_windowHandle, GwlHwndParent, nint.Zero);
        SetPopupToolWindowStyles();
        if (_bridgeHandle != nint.Zero && IsWindow(_bridgeHandle)) _ = DestroyWindow(_bridgeHandle);
        _bridgeHandle = nint.Zero;
        _desktopHostHandle = nint.Zero;
        _ = SetWindowPos(_windowHandle, topmost ? HwndTopmost : HwndNoTopmost,
            bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top,
            SwpNoActivate | SwpFrameChanged | SwpShowWindow);
        _ = ShowWindow(_windowHandle, SwShowNoActivate);
        AppLog.Write($"Detached from desktop bridge. topmost={topmost}.");
    }

    private void CleanupBridgePreservingWindow()
    {
        if (_bridgeHandle == nint.Zero) return;
        if (GetParent(_windowHandle) == _bridgeHandle)
        {
            _ = SetParent(_windowHandle, nint.Zero);
            _ = SetWindowLongPtr(_windowHandle, GwlHwndParent, nint.Zero);
            SetPopupToolWindowStyles();
        }
        if (IsWindow(_bridgeHandle)) _ = DestroyWindow(_bridgeHandle);
        _bridgeHandle = nint.Zero;
        _desktopHostHandle = nint.Zero;
    }

    private void SetPopupToolWindowStyles()
    {
        var style = GetWindowLongPtr(_windowHandle, GwlStyle).ToInt64();
        style = (style | WsPopup) & ~WsChild;
        _ = SetWindowLongPtr(_windowHandle, GwlStyle, new nint(style));
        var exStyle = GetWindowLongPtr(_windowHandle, GwlExStyle).ToInt64();
        exStyle = (exStyle | WsExToolWindow) & ~(WsExAppWindow | WsExLayered);
        _ = SetWindowLongPtr(_windowHandle, GwlExStyle, new nint(exStyle));
    }

    private void SetChildStyles()
    {
        var style = GetWindowLongPtr(_windowHandle, GwlStyle).ToInt64();
        style = (style | WsChild) & ~WsPopup;
        _ = SetWindowLongPtr(_windowHandle, GwlStyle, new nint(style));
        var exStyle = GetWindowLongPtr(_windowHandle, GwlExStyle).ToInt64();
        exStyle = (exStyle | WsExToolWindow) & ~(WsExAppWindow | WsExLayered);
        _ = SetWindowLongPtr(_windowHandle, GwlExStyle, new nint(exStyle));
    }

    private static nint FindDesktopHost(out string strategy)
    {
        strategy = "none";
        var progman = FindWindow("Progman", null);
        nint desktopViewHost = nint.Zero;
        EnumWindows((topLevel, _) =>
        {
            if (!IsUsableDesktopHost(topLevel) || !ContainsDesktopView(topLevel)) return true;
            desktopViewHost = topLevel;
            return false;
        }, nint.Zero);
        if (desktopViewHost != nint.Zero)
        {
            nint worker = nint.Zero;
            while ((worker = FindWindowEx(desktopViewHost, worker, "WorkerW", null)) != nint.Zero)
            {
                if (!IsUsableDesktopHost(worker)) continue;
                strategy = "visible full-size desktop WorkerW";
                return worker;
            }
            strategy = "desktop icon host";
            return desktopViewHost;
        }
        if (IsUsableDesktopHost(progman))
        {
            strategy = "visible Progman";
            return progman;
        }
        return nint.Zero;
    }

    private static bool IsUsableDesktopHost(nint window)
    {
        if (window == nint.Zero || !IsWindow(window) || !IsWindowVisible(window)) return false;
        return GetClientRect(window, out var client)
               && client.Right - client.Left >= 100 && client.Bottom - client.Top >= 100;
    }

    private static bool ContainsDesktopView(nint root)
    {
        if (FindWindowEx(root, nint.Zero, "SHELLDLL_DefView", null) != nint.Zero) return true;
        var found = false;
        EnumChildWindows(root, (child, _) =>
        {
            var className = new StringBuilder(64);
            if (GetClassName(child, className, className.Capacity) > 0
                && string.Equals(className.ToString(), "SHELLDLL_DefView", StringComparison.Ordinal))
            {
                found = true;
                return false;
            }
            return true;
        }, nint.Zero);
        return found;
    }

    private delegate bool EnumWindowsCallback(nint window, nint parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(uint extendedStyle, string className, string windowName,
        uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string? className, string? windowName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint childAfter, string? className, string? windowName);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumChildWindows(nint parent, EnumWindowsCallback callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder className, int maxCount);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetParent(nint child, nint newParent);
    [DllImport("user32.dll")] private static extern nint GetParent(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ClientToScreen(nint window, ref NativePoint point);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint window, out NativeRect bounds);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetClientRect(nint window, out NativeRect bounds);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetLayeredWindowAttributes(nint window, uint colorKey, byte alpha, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }
}

internal readonly record struct ScreenBounds(int Left, int Top, int Right, int Bottom);
