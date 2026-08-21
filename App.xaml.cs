using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using ScreenTranslator.Services;
using ScreenTranslator.Views;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ScreenTranslator;

public partial class App : Application
{
    private NotifyIcon? _notifyIcon;
    private HotkeyService? _hotkeyService;
    private OcrService? _ocrService;
    private TranslationService? _translationService;
    private bool _isCapturing = false;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize Services
        _ocrService = new OcrService();
        _translationService = new TranslationService();
        _hotkeyService = new HotkeyService();

        // Setup Taskbar / System Tray Icon
        SetupNotifyIcon();

        // Register Global Hotkey (Alt + Q)
        try
        {
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
            _hotkeyService.Register();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ホットキー(Alt+Q)の登録に失敗しました: {ex.Message}", "Screen Translator", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SetupNotifyIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = CreateAppIcon(),
            Text = "Screen Translator (Alt + Q で翻訳)",
            Visible = true
        };

        var contextMenu = new ContextMenuStrip();
        
        var itemCapture = new ToolStripMenuItem("📸 キャプチャ＆翻訳 (Alt + Q)", null, (s, e) => StartCapture());
        itemCapture.Font = new Font(itemCapture.Font, System.Drawing.FontStyle.Bold);
        contextMenu.Items.Add(itemCapture);

        contextMenu.Items.Add(new ToolStripSeparator());

        var itemExit = new ToolStripMenuItem("❌ 終了", null, (s, e) => ExitApp());
        contextMenu.Items.Add(itemExit);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                StartCapture();
            }
        };

        _notifyIcon.ShowBalloonTip(3000, "Screen Translator", "起動しました！「Alt + Q」またはアイコンクリックで翻訳を開始できます。", ToolTipIcon.Info);
    }

    private Icon CreateAppIcon()
    {
        // Programmatically generate a crisp 32x32 icon with a translation logo
        using var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Background rounded badge
        using var brush = new SolidBrush(Color.FromArgb(0, 180, 255));
        g.FillEllipse(brush, 2, 2, 28, 28);

        // Text "文" or "T"
        using var textBrush = new SolidBrush(Color.White);
        using var font = new Font("Segoe UI", 12, System.Drawing.FontStyle.Bold);
        var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString("訳", font, textBrush, new RectangleF(0, 0, 32, 32), format);

        var hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    private void OnHotkeyPressed()
    {
        Dispatcher.Invoke(StartCapture);
    }

    private void StartCapture()
    {
        if (_isCapturing) return;
        _isCapturing = true;

        var overlay = new OverlayWindow();
        overlay.Snipped += OnAreaSnipped;
        overlay.Closed += (s, e) => _isCapturing = false;
        overlay.Show();
    }

    private async void OnAreaSnipped(Bitmap bitmap, System.Windows.Point mousePosition)
    {
        var translationWindow = new TranslationWindow();
        translationWindow.SetContent("認識中...", "翻訳しています...");
        translationWindow.ShowAt(mousePosition.X, mousePosition.Y);

        try
        {
            // 1. Perform OCR
            string ocrText = string.Empty;
            if (_ocrService != null)
            {
                ocrText = await _ocrService.RecognizeTextAsync(bitmap);
            }

            if (string.IsNullOrWhiteSpace(ocrText))
            {
                translationWindow.SetContent("(文字が検出されませんでした)", "画面内の文字を認識できませんでした。もう少し広い範囲を選択してみてください。");
                return;
            }

            // 2. Perform Translation
            string translation = string.Empty;
            if (_translationService != null)
            {
                translation = await _translationService.TranslateToJapaneseAsync(ocrText);
            }

            // 3. Update UI
            translationWindow.SetContent(ocrText, translation);
        }
        catch (Exception ex)
        {
            translationWindow.SetContent("エラー", $"処理中にエラーが発生しました: {ex.Message}");
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    private void ExitApp()
    {
        _notifyIcon?.Dispose();
        _hotkeyService?.Dispose();
        _ocrService?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        _hotkeyService?.Dispose();
        _ocrService?.Dispose();
        base.OnExit(e);
    }
}
