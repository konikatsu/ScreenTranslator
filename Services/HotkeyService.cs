using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ScreenTranslator.Services
{
    public enum CaptureMode
    {
        Translate = 1,
        Explain = 2
    }

    public class HotkeyService : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint MOD_ALT = 0x0001;
        private const uint VK_Q = 0x51;
        private const uint VK_W = 0x57;
        
        private const int HOTKEY_ID_TRANSLATE = 9001;
        private const int HOTKEY_ID_EXPLAIN = 9002;

        private HwndSource? _source;
        private readonly List<int> _registeredIds = new List<int>();

        public event Action<CaptureMode>? HotkeyPressed;

        public record HotkeyRegistrationResult(bool TranslateRegistered, bool ExplainRegistered, string? ErrorMessage);

        public HotkeyRegistrationResult Register()
        {
            Dispose(); // 既存の登録とハンドルを破棄
            _source = new HwndSource(new HwndSourceParameters("dummy") { ParentWindow = new IntPtr(-3) }); // HWND_MESSAGE
            _source.AddHook(HwndHook);

            bool translateOk = RegisterHotKey(_source.Handle, HOTKEY_ID_TRANSLATE, MOD_ALT, VK_Q);
            if (translateOk) _registeredIds.Add(HOTKEY_ID_TRANSLATE);

            bool explainOk = RegisterHotKey(_source.Handle, HOTKEY_ID_EXPLAIN, MOD_ALT, VK_W);
            if (explainOk) _registeredIds.Add(HOTKEY_ID_EXPLAIN);

            string? error = null;
            if (!translateOk && !explainOk) error = "Alt+Q と Alt+W の両方の登録に失敗しました。";
            else if (!translateOk) error = "Alt+Q (翻訳) の登録に失敗しました。他のアプリで使用されている可能性があります。";
            else if (!explainOk) error = "Alt+W (AI解説) の登録に失敗しました。他のアプリで使用されている可能性があります。";

            return new HotkeyRegistrationResult(translateOk, explainOk, error);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID_TRANSLATE)
                {
                    HotkeyPressed?.Invoke(CaptureMode.Translate);
                    handled = true;
                }
                else if (id == HOTKEY_ID_EXPLAIN)
                {
                    HotkeyPressed?.Invoke(CaptureMode.Explain);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_source != null)
            {
                _source.RemoveHook(HwndHook);
                foreach (int id in _registeredIds)
                {
                    UnregisterHotKey(_source.Handle, id);
                }
                _source.Dispose();
                _source = null;
                _registeredIds.Clear();
            }
        }
    }
}
