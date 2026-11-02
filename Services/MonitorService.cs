using System.Runtime.InteropServices;
using System.Windows;

namespace CrosshairOverlay.Services;

public readonly record struct MonitorBounds(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;
}

public sealed record MonitorInfo(
    string DeviceName,
    MonitorBounds Bounds,
    MonitorBounds WorkArea,
    bool IsPrimary,
    int Index)
{
    public string DisplayName =>
        $"{Index} · {Bounds.Width}×{Bounds.Height}" + (IsPrimary ? " · основной" : string.Empty);

    public override string ToString() => DisplayName;
}

public static class MonitorService
{
    private const uint MonitorInfoPrimary = 0x00000001;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfn,
        IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
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

    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();

        MonitorEnumProc callback = (monitorHandle, _, _, _) =>
        {
            var info = new MonitorInfoEx
            {
                Size = Marshal.SizeOf<MonitorInfoEx>(),
                DeviceName = string.Empty
            };

            if (!GetMonitorInfoW(monitorHandle, ref info))
            {
                return true;
            }

            monitors.Add(new MonitorInfo(
                info.DeviceName ?? string.Empty,
                new MonitorBounds(
                    info.Monitor.Left,
                    info.Monitor.Top,
                    info.Monitor.Right - info.Monitor.Left,
                    info.Monitor.Bottom - info.Monitor.Top),
                new MonitorBounds(
                    info.Work.Left,
                    info.Work.Top,
                    info.Work.Right - info.Work.Left,
                    info.Work.Bottom - info.Work.Top),
                (info.Flags & MonitorInfoPrimary) != 0,
                0));

            return true;
        };

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);

        return monitors
            .OrderBy(m => m.DeviceName, StringComparer.OrdinalIgnoreCase)
            .Select((m, index) => m with { Index = index + 1 })
            .ToList();
    }

    public static MonitorInfo? GetPrimary()
    {
        var monitors = GetMonitors();
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
    }

    public static MonitorInfo? GetMonitorByDeviceName(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return null;
        }

        return GetMonitors().FirstOrDefault(m =>
            string.Equals(m.DeviceName, deviceName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static MonitorInfo? GetMonitorFromCursor()
    {
        var monitors = GetMonitors();
        if (monitors.Count == 0)
        {
            return null;
        }

        if (GetCursorPos(out var point))
        {
            var match = monitors.FirstOrDefault(m =>
                point.X >= m.Bounds.X &&
                point.X < m.Bounds.Right &&
                point.Y >= m.Bounds.Y &&
                point.Y < m.Bounds.Bottom);

            if (match is not null)
            {
                return match;
            }
        }

        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }

    /// <summary>System DPI scale, used to convert physical monitor coordinates to WPF DIPs.</summary>
    public static double GetSystemScale()
    {
        var primary = GetPrimary();
        if (primary is null || primary.Bounds.Width <= 0)
        {
            return 1.0;
        }

        return Math.Max(0.5, SystemParameters.PrimaryScreenWidth / primary.Bounds.Width);
    }
}
