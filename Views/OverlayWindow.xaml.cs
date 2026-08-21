using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Point = System.Windows.Point;

namespace ScreenTranslator.Views;

public partial class OverlayWindow : Window
{
    private bool _isSelecting = false;
    private Point _startPoint;
    public event Action<Bitmap, Point>? Snipped;

    public OverlayWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Cover all virtual screens (multimonitor support)
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Activate();
        Focus();
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isSelecting = true;
            _startPoint = e.GetPosition(this);

            Canvas.SetLeft(SelectionRect, _startPoint.X);
            Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
            SelectionRect.Visibility = Visibility.Visible;
            CaptureMouse();
        }
    }

    private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isSelecting)
        {
            var current = e.GetPosition(this);
            double x = Math.Min(_startPoint.X, current.X);
            double y = Math.Min(_startPoint.Y, current.Y);
            double w = Math.Abs(current.X - _startPoint.X);
            double h = Math.Abs(current.Y - _startPoint.Y);

            Canvas.SetLeft(SelectionRect, x);
            Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = w;
            SelectionRect.Height = h;
        }
    }

    private async void Window_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isSelecting)
        {
            _isSelecting = false;
            ReleaseMouseCapture();

            var current = e.GetPosition(this);
            double logicalX = Math.Min(_startPoint.X, current.X);
            double logicalY = Math.Min(_startPoint.Y, current.Y);
            double logicalW = Math.Abs(current.X - _startPoint.X);
            double logicalH = Math.Abs(current.Y - _startPoint.Y);

            if (logicalW > 10 && logicalH > 10)
            {
                // Convert WPF logical units to device physical pixels for screen capture
                Point screenTopLeft = PointToScreen(new Point(logicalX, logicalY));
                Point screenBottomRight = PointToScreen(new Point(logicalX + logicalW, logicalY + logicalH));

                int physX = (int)Math.Min(screenTopLeft.X, screenBottomRight.X);
                int physY = (int)Math.Min(screenTopLeft.Y, screenBottomRight.Y);
                int physW = (int)Math.Abs(screenBottomRight.X - screenTopLeft.X);
                int physH = (int)Math.Abs(screenBottomRight.Y - screenTopLeft.Y);

                // Mouse location in WPF logical coordinates for accurate popup placement across all DPI scales
                Point mouseLogicalPos = new Point(Left + current.X, Top + current.Y);

                // Hide overlay immediately so it won't be captured
                Hide();

                // Non-blocking async delay to ensure overlay is fully unrendered before screenshot
                await Task.Delay(60);

                var bitmap = new Bitmap(physW, physH, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(physX, physY, 0, 0, new System.Drawing.Size(physW, physH), CopyPixelOperation.SourceCopy);
                }

                Close();
                Snipped?.Invoke(bitmap, mouseLogicalPos);
            }
            else
            {
                Close();
            }
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
