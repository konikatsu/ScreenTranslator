using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ScreenTranslator.Services
{
    public static class SafeLogger
    {
        private static readonly string LogFilePath;
        private static readonly object _lock = new object();

        static SafeLogger()
        {
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appDir = Path.Combine(localAppData, "ScreenTranslator");
                if (!Directory.Exists(appDir))
                {
                    Directory.CreateDirectory(appDir);
                }
                LogFilePath = Path.Combine(appDir, "debug_startup.log");
            }
            catch
            {
                // Fallback if we can't get LocalAppData for some reason
                LogFilePath = "debug_startup.log";
            }
        }

        public static void Log(string message)
        {
            try
            {
                string sanitizedMessage = Sanitize(message);
                lock (_lock)
                {
                    File.AppendAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {sanitizedMessage}\n");
                }
            }
            catch
            {
                // Ignore logging errors to prevent recursive crashes
            }
        }

        public static void Log(Exception ex, string context = "")
        {
            string message = string.IsNullOrEmpty(context) ? ex.ToString() : $"{context}: {ex}";
            Log(message);
        }

        public static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Mask Google API Keys (starts with AIza)
            string sanitized = Regex.Replace(input, @"AIza[0-9A-Za-z_\-]{30,}", "AIza***REDACTED***");
            
            // Mask URL query parameters named 'key'
            sanitized = Regex.Replace(sanitized, @"key=[^&\s""]+", "key=***");
            
            // Mask long Base64 strings (likely image payloads)
            sanitized = Regex.Replace(sanitized, @"[A-Za-z0-9+/=]{200,}", "[BASE64 OMITTED]");

            return sanitized;
        }
    }
}
