using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using ScreenTranslator.Services;
using ScreenTranslator.Views;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ScreenTranslator
{
    public partial class App : Application
    {
        private NotifyIcon? _notifyIcon;
        private HotkeyService? _hotkeyService;
        private OcrService? _ocrService;
        private TranslationService? _translationService;
        private AiExplainService? _aiExplainService;
        
        private bool _isProcessing = false;
        private readonly CancellationTokenSource _appCts = new CancellationTokenSource();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                SafeLogger.Log($"[AppDomain Unhandled] {args.ExceptionObject}");
            };

            DispatcherUnhandledException += (s, args) =>
            {
                SafeLogger.Log($"[Dispatcher Unhandled] {args.Exception}");
                args.Handled = true;
            };

            try
            {
                SafeLogger.Log($"[Startup] Starting at {DateTime.Now}");

                _ocrService = new OcrService();
                _translationService = new TranslationService();
                _aiExplainService = new AiExplainService();
                _hotkeyService = new HotkeyService();

                SetupNotifyIcon();

                _hotkeyService.HotkeyPressed += OnHotkeyPressed;
                var regResult = _hotkeyService.Register();
                
                if (regResult.ErrorMessage != null)
                {
                    SafeLogger.Log($"[Startup] Hotkey register warning: {regResult.ErrorMessage}");
                    MessageBox.Show(regResult.ErrorMessage, "Screen Translator", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                SafeLogger.Log(ex, "[Startup Error]");
                MessageBox.Show($"起動時エラー: {ex.Message}", "Screen Translator", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupNotifyIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application, // Temp icon
                Text = "Screen Translator",
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();
            
            var itemTranslate = new ToolStripMenuItem("📝 翻訳 (Alt + Q)", null, (s, e) => _ = RunCaptureAsync(CaptureMode.Translate));
            itemTranslate.Font = new Font(itemTranslate.Font, System.Drawing.FontStyle.Bold);
            contextMenu.Items.Add(itemTranslate);

            var itemExplain = new ToolStripMenuItem("🤖 AI解説 (Alt + W)", null, (s, e) => _ = RunCaptureAsync(CaptureMode.Explain));
            contextMenu.Items.Add(itemExplain);

            contextMenu.Items.Add(new ToolStripSeparator());

            var itemSettings = new ToolStripMenuItem("⚙ 設定...", null, (s, e) => ShowSettings());
            contextMenu.Items.Add(itemSettings);

            contextMenu.Items.Add(new ToolStripSeparator());

            var itemExit = new ToolStripMenuItem("❌ 終了", null, (s, e) => ExitApp());
            contextMenu.Items.Add(itemExit);

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void ShowSettings()
        {
            new SettingsWindow().Show();
        }

        private void OnHotkeyPressed(CaptureMode mode)
        {
            Dispatcher.Invoke(() => _ = RunCaptureAsync(mode));
        }

        private async Task RunCaptureAsync(CaptureMode mode)
        {
            if (_isProcessing) return;
            _isProcessing = true;
            Bitmap? capturedBitmap = null;

            try
            {
                if (mode == CaptureMode.Explain)
                {
                    var settings = SettingsManager.LoadSettings();
                    if (string.IsNullOrEmpty(settings.EncryptedGeminiApiKey))
                    {
                        ShowSettings();
                        return; // user needs to set API key first
                    }
                }

                var overlay = new OverlayWindow();
                var tcs = new TaskCompletionSource<(Bitmap Bitmap, System.Windows.Point Position)?>(TaskCreationOptions.RunContinuationsAsynchronously);
                
                // Overlay completion handlers
                overlay.Snipped += (bmp, pos) => tcs.TrySetResult((bmp, pos));
                overlay.Closed += (s, e) => tcs.TrySetResult(null);

                using var reg = _appCts.Token.Register(() => { tcs.TrySetResult(null); overlay.Close(); });
                
                overlay.Show();
                var result = await tcs.Task;
                // DO NOT overlay.Close() here; OverlayWindow closes itself, avoiding double close InvalidOperationException

                if (result == null) return; // Esc or Cancelled

                capturedBitmap = result.Value.Bitmap;
                var pos = result.Value.Position;

                var resultWindow = new TranslationWindow();
                resultWindow.SetLoadingMode("⏳ 処理中...", "AIに問い合わせ中...");
                resultWindow.ShowAt(pos.X, pos.Y);

                var opCts = CancellationTokenSource.CreateLinkedTokenSource(_appCts.Token);
                EventHandler closedHandler = null!;
                closedHandler = (s, e) => 
                {
                    resultWindow.Closed -= closedHandler;
                    try { if (!opCts.IsCancellationRequested) opCts.Cancel(); } catch { }
                    opCts.Dispose();
                };
                resultWindow.Closed += closedHandler;

                try
                {
                    if (mode == CaptureMode.Translate)
                    {
                        string ocrText = await _ocrService!.RecognizeTextAsync(capturedBitmap);
                        if (string.IsNullOrWhiteSpace(ocrText))
                        {
                            resultWindow.SetTranslateMode("(なし)", "文字が検出されませんでした。");
                        }
                        else
                        {
                            string translation = await _translationService!.TranslateToJapaneseAsync(ocrText, opCts.Token);
                            resultWindow.SetTranslateMode(ocrText, translation);
                        }
                    }
                    else if (mode == CaptureMode.Explain)
                    {
                        string explanation = await _aiExplainService!.ExplainAsync(capturedBitmap, opCts.Token);
                        resultWindow.SetExplainMode(explanation);
                    }
                }
                catch (OperationCanceledException)
                {
                    // User closed the window while processing, silently exit
                }
                catch (Exception ex)
                {
                    SafeLogger.Log(ex, "Error during processing");
                    resultWindow.SetLoadingMode("❌ エラー", ex.Message);
                }
            }
            catch (Exception ex)
            {
                SafeLogger.Log(ex, "Fatal error in RunCaptureAsync");
            }
            finally
            {
                capturedBitmap?.Dispose();
                _isProcessing = false;
            }
        }

        private void ExitApp()
        {
            _appCts.Cancel();
            _notifyIcon?.Dispose();
            _hotkeyService?.Dispose();
            _ocrService?.Dispose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ExitApp();
            base.OnExit(e);
        }
    }
}
