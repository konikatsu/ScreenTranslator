using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace ScreenTranslator.Views
{
    public partial class TranslationWindow : Window
    {
        private bool _isManualResized = false;

        public TranslationWindow()
        {
            InitializeComponent();
        }

        public void SetTranslateMode(string original, string translated)
        {
            TxtHeader.Text = "📝 翻訳";
            OriginalSection.Visibility = Visibility.Visible;
            TxtOriginal.Text = original;
            TxtTranslation.Text = translated;
            
            if (!_isManualResized)
            {
                this.Width = 380;
                ScrollTranslation.MaxHeight = 250;
            }
            EnsureInBounds();
        }

        public void SetExplainMode(string explanation)
        {
            TxtHeader.Text = "🤖 AI解説";
            OriginalSection.Visibility = Visibility.Collapsed;
            TxtTranslation.Text = explanation;
            
            if (!_isManualResized)
            {
                this.Width = 480;
                ScrollTranslation.MaxHeight = 450;
            }
            EnsureInBounds();
        }

        public void SetLoadingMode(string title, string message)
        {
            TxtHeader.Text = title;
            OriginalSection.Visibility = Visibility.Collapsed;
            TxtTranslation.Text = message;
        }

        public void SetContent(string original, string translated)
        {
            SetTranslateMode(original, translated);
        }

        public void ShowAt(double x, double y)
        {
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            
            var helper = new WindowInteropHelper(this);
            helper.EnsureHandle();

            double dpiScaleX = 1.0;
            double dpiScaleY = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            int physX = (int)(x * dpiScaleX);
            int physY = (int)(y * dpiScaleY);
            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(physX, physY));

            // Use WorkingArea to avoid taskbar overlap
            double screenLeftDIP = screen.WorkingArea.Left / dpiScaleX;
            double screenTopDIP = screen.WorkingArea.Top / dpiScaleY;
            double screenRightDIP = screen.WorkingArea.Right / dpiScaleX;
            double screenBottomDIP = screen.WorkingArea.Bottom / dpiScaleY;

            double targetWidth = double.IsNaN(this.Width) ? 380 : this.Width;
            double targetHeight = 220; 

            double targetX = x;
            double targetY = y + 20;

            if (targetX + targetWidth > screenRightDIP) targetX = screenRightDIP - targetWidth - 10;
            if (targetY + targetHeight > screenBottomDIP) targetY = screenBottomDIP - targetHeight - 20;
            if (targetX < screenLeftDIP) targetX = screenLeftDIP + 10;
            if (targetY < screenTopDIP) targetY = screenTopDIP + 10;

            this.Left = targetX;
            this.Top = targetY;
            this.Show();
        }
        
        private void EnsureInBounds()
        {
            if (!this.IsLoaded) return;
            var helper = new WindowInteropHelper(this);
            if (helper.Handle == IntPtr.Zero) return;
            
            var source = PresentationSource.FromVisual(this);
            if (source == null) return;
            
            double dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
            double dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
            
            Dispatcher.BeginInvoke(new Action(() => 
            {
                int physX = (int)(this.Left * dpiScaleX);
                int physY = (int)(this.Top * dpiScaleY);
                var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(physX, physY));
                
                double screenLeftDIP = screen.WorkingArea.Left / dpiScaleX;
                double screenTopDIP = screen.WorkingArea.Top / dpiScaleY;
                double screenRightDIP = screen.WorkingArea.Right / dpiScaleX;
                double screenBottomDIP = screen.WorkingArea.Bottom / dpiScaleY;
                
                bool changed = false;
                double targetX = this.Left;
                double targetY = this.Top;

                if (targetX + this.ActualWidth > screenRightDIP)
                {
                    targetX = screenRightDIP - this.ActualWidth - 10;
                    changed = true;
                }
                if (targetX < screenLeftDIP)
                {
                    targetX = screenLeftDIP + 10;
                    changed = true;
                }
                
                if (targetY + this.ActualHeight > screenBottomDIP)
                {
                    targetY = screenBottomDIP - this.ActualHeight - 20;
                    changed = true;
                }
                if (targetY < screenTopDIP)
                {
                    targetY = screenTopDIP + 10;
                    changed = true;
                }

                if (changed)
                {
                    this.Left = targetX;
                    this.Top = targetY;
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string textToCopy = OriginalSection.Visibility == Visibility.Visible
                    ? $"{TxtOriginal.Text}\n\n{TxtTranslation.Text}"
                    : TxtTranslation.Text;
                
                if (!string.IsNullOrEmpty(textToCopy))
                {
                    System.Windows.Clipboard.SetText(textToCopy);
                    BtnCopy.Content = "✓ コピー済";
                    await System.Threading.Tasks.Task.Delay(2000);
                    BtnCopy.Content = "📋 コピー";
                }
            }
            catch (Exception ex)
            {
                Services.SafeLogger.Log(ex, "Failed to copy to clipboard.");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }

        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (!_isManualResized)
            {
                _isManualResized = true;
                this.SizeToContent = SizeToContent.Manual;
                this.Width = this.ActualWidth;
                this.Height = this.ActualHeight;
                ScrollTranslation.MaxHeight = double.PositiveInfinity;
            }

            double newWidth = this.Width + e.HorizontalChange;
            double newHeight = this.Height + e.VerticalChange;

            if (!double.IsNaN(newWidth) && newWidth > 200) this.Width = newWidth;
            if (!double.IsNaN(newHeight) && newHeight > 120) this.Height = newHeight;
        }
    }
}
