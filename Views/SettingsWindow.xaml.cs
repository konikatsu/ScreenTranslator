using System;
using System.Windows;
using ScreenTranslator.Services;

namespace ScreenTranslator.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            var settings = SettingsManager.LoadSettings();
            string? rawKey = SettingsManager.DecryptApiKey(settings.EncryptedGeminiApiKey);
            if (!string.IsNullOrEmpty(rawKey))
            {
                // Display placeholder instead of real key
                PwdApiKey.Password = "••••••••••••••••••••••••••••••••••••••";
            }
            TxtModel.Text = settings.GeminiModel;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = SettingsManager.LoadSettings();
                string rawKey = PwdApiKey.Password.Trim();
                
                if (string.IsNullOrWhiteSpace(rawKey))
                {
                    settings.EncryptedGeminiApiKey = null;
                }
                else if (rawKey != "••••••••••••••••••••••••••••••••••••••")
                {
                    settings.EncryptedGeminiApiKey = SettingsManager.EncryptApiKey(rawKey);
                }

                if (!string.IsNullOrWhiteSpace(TxtModel.Text))
                {
                    settings.GeminiModel = TxtModel.Text.Trim();
                }

                SettingsManager.SaveSettings(settings);
                this.Close();
            }
            catch (Exception ex)
            {
                string safeEx = SafeLogger.Sanitize(ex.Message);
                System.Windows.MessageBox.Show($"設定の保存に失敗しました: {safeEx}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

