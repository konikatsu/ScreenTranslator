using System;
using System.IO;

namespace ScreenTranslator;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        string logPath = @"C:\dev\ScreenTranslator\debug_startup.log";
        try
        {
            File.AppendAllText(logPath, $"[Main Entry] Starting process at {DateTime.Now}, PID: {Environment.ProcessId}\n");
            
            var app = new App();
            app.InitializeComponent();
            File.AppendAllText(logPath, "[Main Entry] App initialized, calling app.Run()...\n");
            app.Run();
            File.AppendAllText(logPath, "[Main Entry] app.Run() exited normally.\n");
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"[Main Fatal Error] {ex}\n");
            System.Windows.MessageBox.Show($"致命的なエラー: {ex.Message}", "Screen Translator", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
