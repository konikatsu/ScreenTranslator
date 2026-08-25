using System;
using System.IO;

namespace ScreenTranslator;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            ScreenTranslator.Services.SafeLogger.Log($"[Main Entry] Starting process at {DateTime.Now}, PID: {Environment.ProcessId}");
            
            var app = new App();
            app.InitializeComponent();
            ScreenTranslator.Services.SafeLogger.Log("[Main Entry] App initialized, calling app.Run()...");
            app.Run();
            ScreenTranslator.Services.SafeLogger.Log("[Main Entry] app.Run() exited normally.");
        }
        catch (Exception ex)
        {
            ScreenTranslator.Services.SafeLogger.Log(ex, "[Main Fatal Error]");
            System.Windows.MessageBox.Show($"致命的なエラー: {ex.Message}", "Screen Translator", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
