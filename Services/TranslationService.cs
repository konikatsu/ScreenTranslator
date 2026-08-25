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

        // Send the entire text as a single request to avoid hitting rate limits with concurrent requests.
        // Google Translate API handles newlines (\n) correctly.
        return await TranslateSingleBlockAsync(text.Trim());
    }

    private async Task<string> TranslateSingleBlockAsync(string text)
    {
        try
        {
            // Use dict-chrome-ex which is often more stable and less aggressively rate-limited than gtx
            string url = $"https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=en&tl=ja&q={Uri.EscapeDataString(text)}";
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            // Spoof Chrome extension user agent
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();

            // dict-chrome-ex returns a simple JSON array of translated strings if the input has newlines,
            // or a single array with one string.
            // Example: ["こんにちは\n世界"] or ["こんにちは", "世界"]
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            
            if (root.ValueKind == JsonValueKind.Array)
            {
                var translatedBuilder = new System.Text.StringBuilder();
                foreach (var element in root.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        translatedBuilder.Append(element.GetString());
                    }
                }
                string result = translatedBuilder.ToString();
                return string.IsNullOrWhiteSpace(result) ? text : result.Trim();
            }

            return text;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Translation Error] {ex.Message}");
            return text; // Fallback to original text on failure
        }
    }
}
