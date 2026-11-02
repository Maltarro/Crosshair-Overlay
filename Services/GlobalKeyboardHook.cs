using System.ComponentModel;
using System.Runtime.InteropServices;
using CrosshairOverlay.Interop;

namespace CrosshairOverlay.Services;

/// <summary>
/// Low-level global keyboard hook (WH_KEYBOARD_LL). Fires once per key press
/// and always lets the key event pass through to the focused application.
/// </summary>
public sealed class GlobalKeyboardHook : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _callback;
    private readonly HashSet<int> _pressedKeys = new();
    private IntPtr _hookHandle;
    private bool _disposed;

    public GlobalKeyboardHook()
    {
        _callback = HookCallback;

        var module = NativeMethods.GetModuleHandleW(null);
        _hookHandle = NativeMethods.SetWindowsHookExW(
            NativeMethods.WH_KEYBOARD_LL,
            _callback,
            module,
            0);

        if (_hookHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось установить глобальный хук клавиатуры.");
        }
    }

    /// <summary>Raised on the UI thread with a Win32 virtual-key code.</summary>
    public event EventHandler<int>? KeyPressed;

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var message = wParam.ToInt32();
                var isDown = message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
                var isUp = message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;

                if (isDown || isUp)
                {
                    var virtualKey = Marshal.ReadInt32(lParam);

                    if (isDown && _pressedKeys.Add(virtualKey))
                    {
                        KeyPressed?.Invoke(this, virtualKey);
                    }
                    else if (isUp)
                    {
                        _pressedKeys.Remove(virtualKey);
                    }
                }
            }
        }
        catch
        {
            // A hook callback must never throw; ignore any failure and pass the event on.
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
