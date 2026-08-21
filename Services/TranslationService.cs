using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ScreenTranslator.Services;

public class TranslationService
{
    private readonly HttpClient _httpClient;

    public TranslationService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    }

    /// <summary>
    /// Translates text from English (or auto-detected source) to Japanese using Google Translate Web API.
    /// </summary>
    public async Task<string> TranslateToJapaneseAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        try
        {
            string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=ja&dt=t&q={Uri.EscapeDataString(text)}";
            string response = await _httpClient.GetStringAsync(url);

            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var sentences = root[0];
                var translatedBuilder = new System.Text.StringBuilder();

                foreach (var sentence in sentences.EnumerateArray())
                {
                    if (sentence.ValueKind == JsonValueKind.Array && sentence.GetArrayLength() > 0)
                    {
                        translatedBuilder.Append(sentence[0].GetString());
                    }
                }

                return translatedBuilder.ToString();
            }

            return "翻訳結果を取得できませんでした。";
        }
        catch (Exception ex)
        {
            return $"翻訳エラー: {ex.Message}";
        }
    }
}
