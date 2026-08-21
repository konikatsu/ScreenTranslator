using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ScreenTranslator.Services;

public class HotkeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    public const uint VK_Q = 0x51;

    private const int HOTKEY_ID = 9001;
    private const int WM_HOTKEY = 0x0312;

    private HwndSource? _hwndSource;
    public event Action? HotkeyPressed;

    public void Register()
    {
        var parameters = new HwndSourceParameters("ScreenTranslatorHotkeyListener")
        {
            HwndSourceHook = HwndHook,
            ParentWindow = new IntPtr(-3) // HWND_MESSAGE
        };

        _hwndSource = new HwndSource(parameters);

        // Try Register Alt + Q with MOD_NOREPEAT
        bool success = RegisterHotKey(_hwndSource.Handle, HOTKEY_ID, MOD_ALT | MOD_NOREPEAT, VK_Q);
        if (!success)
        {
            // Fallback without MOD_NOREPEAT
            success = RegisterHotKey(_hwndSource.Handle, HOTKEY_ID, MOD_ALT, VK_Q);
        }

        if (!success)
        {
            throw new InvalidOperationException("ホットキー(Alt+Q)の登録に失敗しました。他のアプリケーションが既に使用している可能性があります。");
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_hwndSource != null)
        {
            UnregisterHotKey(_hwndSource.Handle, HOTKEY_ID);
            _hwndSource.Dispose();
            _hwndSource = null;
        }
    }
}
