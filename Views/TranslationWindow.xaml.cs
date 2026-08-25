using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
                this.MaxHeight = 350;
            }
        }

        public void SetExplainMode(string explanation)
        {
            TxtHeader.Text = "🤖 AI解説";
            OriginalSection.Visibility = Visibility.Collapsed;
            TxtTranslation.Text = explanation;
            
            if (!_isManualResized)
            {
                this.Width = 480;
                this.MaxHeight = 520;
            }
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
            
            double targetWidth = this.Width > 0 ? this.Width : 380;
            double targetHeight = 220;
            
            // Get DPI scale
            double dpiScaleX = 1.0;
            double dpiScaleY = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            // Convert logical position (DIP) to physical screen point for Screen.FromPoint
            int physX = (int)(x * dpiScaleX);
            int physY = (int)(y * dpiScaleY);
            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(physX, physY));

            // Convert monitor bounds back to DIP
            double screenLeftDIP = screen.Bounds.Left / dpiScaleX;
            double screenTopDIP = screen.Bounds.Top / dpiScaleY;
            double screenRightDIP = screen.Bounds.Right / dpiScaleX;
            double screenBottomDIP = screen.Bounds.Bottom / dpiScaleY;

            double targetX = x;
            double targetY = y + 20;

            if (targetX + targetWidth > screenRightDIP) targetX = screenRightDIP - targetWidth - 10;
            if (targetY + targetHeight > screenBottomDIP) targetY = y - targetHeight - 20;
            if (targetX < screenLeftDIP) targetX = screenLeftDIP + 10;
            if (targetY < screenTopDIP) targetY = screenTopDIP + 10;

            this.Left = targetX;
            this.Top = targetY;
            this.Show();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
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
                this.MaxHeight = double.PositiveInfinity;
            }

            double newWidth = this.Width + e.HorizontalChange;
            double newHeight = this.Height + e.VerticalChange;

            if (newWidth > 200) this.Width = newWidth;
            if (newHeight > 120) this.Height = newHeight;
        }
    }
}


