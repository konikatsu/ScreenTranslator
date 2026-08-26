using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ScreenTranslator.Services
{
    public class AppSettings
    {
        public string? EncryptedGeminiApiKey { get; set; }
        public string GeminiModel { get; set; } = "gemini-2.5-flash"; // gemini-2.5-flash is currently the stable version
    }

    public static class SettingsManager
    {
        private static readonly string SettingsDirPath;
        private static readonly string SettingsFilePath;

        static SettingsManager()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            SettingsDirPath = Path.Combine(appData, "ScreenTranslator");
            SettingsFilePath = Path.Combine(SettingsDirPath, "settings.json");
        }

        public static AppSettings LoadSettings()
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }

            try
            {
                string json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                return settings ?? new AppSettings();
            }
            catch (JsonException ex)
            {
                SafeLogger.Log(ex, "JSON is malformed. Backing up and returning default.");
                BackupCorruptFile();
                return new AppSettings();
            }
            catch (Exception ex)
            {
                SafeLogger.Log(ex, "Failed to load settings (e.g. file lock). Returning default.");
                return new AppSettings();
            }
        }

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                if (!Directory.Exists(SettingsDirPath))
                {
                    Directory.CreateDirectory(SettingsDirPath);
                }

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                string tmpPath = SettingsFilePath + ".tmp";

                // Atomic save
                using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(fs, Encoding.UTF8))
                {
                    writer.Write(json);
                    writer.Flush();
                    fs.Flush(true); // flushToDisk
                }

                if (File.Exists(SettingsFilePath))
                {
                    File.Replace(tmpPath, SettingsFilePath, null);
                }
                else
                {
                    File.Move(tmpPath, SettingsFilePath);
                }
            }
            catch (Exception ex)
            {
                SafeLogger.Log(ex, "Failed to save settings.");
                throw;
            }
        }

        private static void BackupCorruptFile()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string backupPath = $"{SettingsFilePath}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
                    File.Move(SettingsFilePath, backupPath);
                }
            }
            catch (Exception ex)
            {
                SafeLogger.Log(ex, "Failed to backup corrupt settings file.");
            }
        }

        public static string? DecryptApiKey(string? encryptedKeyBase64)
        {
            if (string.IsNullOrEmpty(encryptedKeyBase64)) return null;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedKeyBase64);
                byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (CryptographicException ex)
            {
                SafeLogger.Log(ex, "DPAPI Decryption failed.");
                return null;
            }
            catch (Exception ex)
            {
                SafeLogger.Log(ex, "Failed to decrypt API key.");
                return null;
            }
        }

        public static string? EncryptApiKey(string? rawKey)
        {
            if (string.IsNullOrEmpty(rawKey)) return null;

            try
            {
                byte[] rawBytes = Encoding.UTF8.GetBytes(rawKey);
                byte[] encryptedBytes = ProtectedData.Protect(rawBytes, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                SafeLogger.Log(ex, "Failed to encrypt API key.");
                throw;
            }
        }
    }
}

