using System.Runtime.InteropServices;

namespace CrosshairOverlay.Services;

/// <summary>
/// Detects whether the foreground application currently occupies a whole monitor.
/// Uses the DWM visible frame bounds so ordinary maximized windows (which leave the
/// taskbar visible) are not treated as fullscreen.
/// </summary>
public static class FullscreenDetector
{
    private const uint MonitorDefaultToNearest = 0x00000002;

    private const int DwmwaExtendedFrameBounds = 9;
    private const int DwmwaCloaked = 14;

    // Physical-pixel tolerance for slightly off-by-one window bounds.
    private const int Tolerance = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MonitorInfoEx monitorInfo);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr hWnd, char[] lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out Rect value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out int value, int size);

    private static readonly HashSet<string> ShellWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "MultitaskingViewFrame",
        "ForegroundStaging"
    };

    /// <summary>True when the window in the foreground fills an entire monitor.</summary>
    public static bool IsForegroundFullscreen()
    {
        try
        {
            var handle = GetForegroundWindow();
            if (handle == IntPtr.Zero || !IsWindowVisible(handle) || IsIconic(handle))
            {
                return false;
            }

            // Never let our own windows (settings, overlay) trigger the crosshair.
            GetWindowThreadProcessId(handle, out var processId);
            if (processId == (uint)Environment.ProcessId)
            {
                return false;
            }

            if (IsShellWindow(handle) || IsCloaked(handle))
            {
                return false;
            }

            var bounds = GetVisibleBounds(handle);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return false;
            }

            var monitor = GetMonitorBounds(handle);
            if (monitor is null)
            {
                return false;
            }

            var area = monitor.Value;
            return bounds.Left <= area.Left + Tolerance
                   && bounds.Top <= area.Top + Tolerance
                   && bounds.Right >= area.Right - Tolerance
                   && bounds.Bottom >= area.Bottom - Tolerance;
        }
        catch
        {
            // Detection is best-effort: on any failure fall back to "not fullscreen".
            return false;
        }
    }

    private static bool IsShellWindow(IntPtr handle)
    {
        var buffer = new char[256];
        var length = GetClassNameW(handle, buffer, buffer.Length);
        if (length <= 0)
        {
            return false;
        }

        return ShellWindowClasses.Contains(new string(buffer, 0, length));
    }

    private static bool IsCloaked(IntPtr handle)
    {
        if (DwmGetWindowAttribute(handle, DwmwaCloaked, out int cloaked, sizeof(int)) != 0)
        {
            return false;
        }

        return cloaked != 0;
    }

    private static Rect GetVisibleBounds(IntPtr handle)
    {
        // The extended frame bounds exclude the invisible resize borders that make a
        // maximized window look slightly larger than its monitor.
        if (DwmGetWindowAttribute(handle, DwmwaExtendedFrameBounds, out Rect bounds, Marshal.SizeOf<Rect>()) == 0
            && bounds.Width > 0
            && bounds.Height > 0)
        {
            return bounds;
        }

        return GetWindowRect(handle, out bounds) ? bounds : default;
    }

    private static Rect? GetMonitorBounds(IntPtr handle)
    {
        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return null;
        }

        var info = new MonitorInfoEx
        {
            Size = Marshal.SizeOf<MonitorInfoEx>(),
            DeviceName = string.Empty
        };

        return GetMonitorInfoW(monitor, ref info) ? info.Monitor : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;

        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }
}
