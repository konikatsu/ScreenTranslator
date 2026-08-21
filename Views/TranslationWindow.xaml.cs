using System;
using System.Windows;
using System.Windows.Input;
using Clipboard = System.Windows.Clipboard;

namespace ScreenTranslator.Views;

public partial class TranslationWindow : Window
{
    public TranslationWindow()
    {
        InitializeComponent();
    }

    public void SetContent(string original, string translation)
    {
        TxtOriginal.Text = string.IsNullOrWhiteSpace(original) ? "(テキストを検出できませんでした)" : original;
        TxtTranslation.Text = string.IsNullOrWhiteSpace(translation) ? "(翻訳できませんでした)" : translation;
    }

    public void ShowAt(double targetX, double targetY)
    {
        // Position window near cursor, adjusting to stay on screen
        double screenWidth = SystemParameters.VirtualScreenWidth;
        double screenHeight = SystemParameters.VirtualScreenHeight;
        double screenLeft = SystemParameters.VirtualScreenLeft;
        double screenTop = SystemParameters.VirtualScreenTop;

        double desiredLeft = targetX + 15;
        double desiredTop = targetY + 15;

        // Estimate size if not yet rendered
        double estimatedWidth = 380;
        double estimatedHeight = 220;

        if (desiredLeft + estimatedWidth > screenLeft + screenWidth)
        {
            desiredLeft = targetX - estimatedWidth - 10;
        }

        if (desiredTop + estimatedHeight > screenTop + screenHeight)
        {
            desiredTop = targetY - estimatedHeight - 10;
        }

        Left = Math.Max(screenLeft, desiredLeft);
        Top = Math.Max(screenTop, desiredTop);

        Show();
        Activate();
    }

    // Enable drag to move the window
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    // Close on Escape key
    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(TxtTranslation.Text))
            {
                Clipboard.SetText(TxtTranslation.Text);
                BtnCopy.Content = "✓ 完了";
            }
        }
        catch
        {
            // Clipboard error handling
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
