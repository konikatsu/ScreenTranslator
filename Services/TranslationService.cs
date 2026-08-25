using System;
using System.Drawing;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenTranslator.Services
{
    public class TranslationService
    {
        private readonly HttpClient _httpClient = new HttpClient();

        public async Task<string> TranslateToJapaneseAsync(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return await TranslateSingleBlockAsync(text.Trim(), cancellationToken);
        }

        private async Task<string> TranslateSingleBlockAsync(string text, CancellationToken cancellationToken)
        {
            try
            {
                string url = $"https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=en&tl=ja&q={Uri.EscapeDataString(text)}";
                
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                
                // Parse JSON array of arrays: [["translated text", "original text", ...]]
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var firstElement = root[0];
                    if (firstElement.ValueKind == JsonValueKind.Array && firstElement.GetArrayLength() > 0)
                    {
                        var translatedText = firstElement[0].GetString();
                        if (!string.IsNullOrEmpty(translatedText))
                        {
                            return translatedText;
                        }
                    }
                }
                
                return "(翻訳をパースできませんでした)";
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return $"(翻訳エラー: {ex.Message})";
            }
        }
    }
}
