using System;
using System.Windows;
using System.Windows.Forms;
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

    private void Window_Deactivated(object sender, EventArgs e)
    {
        Close();
    }
}
