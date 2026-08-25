using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

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
                this.MaxWidth = 480;
                ScrollTranslation.MaxHeight = 200;
            }
        }

        public void SetExplainMode(string explanation)
        {
            TxtHeader.Text = "🤖 AI解説";
            OriginalSection.Visibility = Visibility.Collapsed;
            TxtTranslation.Text = explanation;
            
            if (!_isManualResized)
            {
                this.MaxWidth = 560;
                ScrollTranslation.MaxHeight = 480;
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
            
            double estWidth = 380;
            double estHeight = 220;
            
            double targetX = x;
            double targetY = y + 20;
            
            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)x, (int)y));
            
            if (targetX + estWidth > screen.Bounds.Right) targetX = screen.Bounds.Right - estWidth;
            if (targetY + estHeight > screen.Bounds.Bottom) targetY = screen.Bounds.Bottom - estHeight - 20;
            if (targetX < screen.Bounds.Left) targetX = screen.Bounds.Left;
            if (targetY < screen.Bounds.Top) targetY = screen.Bounds.Top;
            
            this.Left = targetX;
            this.Top = targetY;
            this.Show();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (!_isManualResized)
            {
                _isManualResized = true;
                this.SizeToContent = SizeToContent.Manual;
                this.Width = this.ActualWidth;
                this.Height = this.ActualHeight;
                this.MaxWidth = double.PositiveInfinity;
                ScrollTranslation.MaxHeight = double.PositiveInfinity;
            }

            double newWidth = this.Width + e.HorizontalChange;
            double newHeight = this.Height + e.VerticalChange;

            if (newWidth > 150) this.Width = newWidth;
            if (newHeight > 100) this.Height = newHeight;
        }
    }
}
