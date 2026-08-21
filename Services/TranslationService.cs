using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Translates text from English to Japanese using Google Translate Web API.
    /// Preserves multi-line structure and translates lines in parallel with Task.WhenAll.
    /// </summary>
    public async Task<string> TranslateToJapaneseAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        if (lines.Length <= 1)
        {
            return await TranslateSingleBlockAsync(text.Trim());
        }

        // Translate each line concurrently in parallel for blazing fast response
        var tasks = lines
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(TranslateSingleBlockAsync);

        var translatedLines = await Task.WhenAll(tasks);
        return string.Join(Environment.NewLine, translatedLines);
    }

    private async Task<string> TranslateSingleBlockAsync(string text)
    {
        try
        {
            // Explicitly set source as English (sl=en) to prevent false Japanese detection
            string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ja&dt=t&q={Uri.EscapeDataString(text)}";
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

                string result = translatedBuilder.ToString();
                return string.IsNullOrWhiteSpace(result) ? text : result;
            }

            return text;
        }
        catch
        {
            // Fallback with auto-detect if sl=en fails
            try
            {
                string fallbackUrl = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=ja&dt=t&q={Uri.EscapeDataString(text)}";
                string fbResponse = await _httpClient.GetStringAsync(fallbackUrl);
                using var fbDoc = JsonDocument.Parse(fbResponse);
                var fbRoot = fbDoc.RootElement;
                if (fbRoot.ValueKind == JsonValueKind.Array && fbRoot.GetArrayLength() > 0)
                {
                    return fbRoot[0][0][0].GetString() ?? text;
                }
            }
            catch {}

            return text;
        }
    }
}
