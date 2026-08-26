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
        private bool _isShuttingDown = false;
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
                string safeMsg = SafeLogger.Sanitize(args.Exception.Message); MessageBox.Show($"予期せぬエラーが発生しました: {safeMsg}", "Screen Translator エラー", MessageBoxButton.OK, MessageBoxImage.Error);
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
                string safeEx = SafeLogger.Sanitize(ex.Message); MessageBox.Show($"起動時エラー: {safeEx}", "Screen Translator", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupNotifyIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "Screen Translator",
                Visible = true
            };

            _notifyIcon.DoubleClick += (s, e) => _ = RunCaptureAsync(CaptureMode.Translate);

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
            foreach (Window window in Current.Windows)
            {
                if (window is SettingsWindow)
                {
                    window.Activate();
                    return;
                }
            }
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
                        return;
                    }
                }

                var overlay = new OverlayWindow();
                var tcs = new TaskCompletionSource<(Bitmap Bitmap, System.Windows.Point Position)?>(TaskCreationOptions.RunContinuationsAsynchronously);
                
                overlay.Snipped += (bmp, pos) =>
                {
                    if (!tcs.TrySetResult((bmp, pos)))
                    {
                        bmp.Dispose();
                    }
                };
                overlay.Closed += (s, e) => tcs.TrySetResult(null);

                using var reg = _appCts.Token.Register(() => { tcs.TrySetResult(null); overlay.Close(); });
                
                overlay.Show();
                var result = await tcs.Task;

                if (result == null) return;

                capturedBitmap = result.Value.Bitmap;
                var pos = result.Value.Position;

                var resultWindow = new TranslationWindow();
                resultWindow.SetLoadingMode("⏳ 処理中...", "少々お待ちください...");
                resultWindow.ShowAt(pos.X, pos.Y);

                var opCts = CancellationTokenSource.CreateLinkedTokenSource(_appCts.Token);
                EventHandler closedHandler = null!;
                closedHandler = (s, e) => 
                {
                    try { if (!opCts.IsCancellationRequested) opCts.Cancel(); } catch { }
                };
                resultWindow.Closed += closedHandler;

                try
                {
                    if (mode == CaptureMode.Translate)
                    {
                        string ocrText = await _ocrService!.RecognizeTextAsync(capturedBitmap);
                        opCts.Token.ThrowIfCancellationRequested();

                        if (string.IsNullOrWhiteSpace(ocrText))
                        {
                            if (resultWindow.IsLoaded) resultWindow.SetTranslateMode("(なし)", "文字が検出されませんでした。");
                        }
                        else
                        {
                            string translation = await _translationService!.TranslateToJapaneseAsync(ocrText, opCts.Token);
                            if (resultWindow.IsLoaded) resultWindow.SetTranslateMode(ocrText, translation);
                        }
                    }
                    else if (mode == CaptureMode.Explain)
                    {
                        string explanation = await _aiExplainService!.ExplainAsync(capturedBitmap, opCts.Token);
                        if (resultWindow.IsLoaded) resultWindow.SetExplainMode(explanation);
                    }
                }
                catch (OperationCanceledException)
                {
                    // User closed the window while processing, silently exit
                }
                catch (Exception ex)
                {
                    SafeLogger.Log(ex, "Error during processing");
                    if (resultWindow.IsLoaded) resultWindow.SetLoadingMode("❌ エラー", SafeLogger.Sanitize(ex.Message));
                }
                finally
                {
                    resultWindow.Closed -= closedHandler;
                    opCts.Dispose();
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
            if (_isShuttingDown) return;
            _isShuttingDown = true;
            
            try { _appCts.Cancel(); } catch { }
            // Let the process exit clean up the CTS to avoid races with inflight operations.
            
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            
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


