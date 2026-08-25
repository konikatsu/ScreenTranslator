using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenTranslator.Services
{
    public class AiExplainService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<string> ExplainAsync(Bitmap originalBitmap, CancellationToken cancellationToken)
        {
            var settings = SettingsManager.LoadSettings();
            string? apiKey = SettingsManager.DecryptApiKey(settings.EncryptedGeminiApiKey);

            if (string.IsNullOrEmpty(apiKey))
            {
                return "APIキーが設定されていません。システムトレイのアイコンから「設定...」を開いてGemini APIキーを登録してください。";
            }

            string modelName = string.IsNullOrWhiteSpace(settings.GeminiModel) ? "gemini-3.7-flash" : settings.GeminiModel;
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent";

            string base64Image;
            try
            {
                // Resize if too large
                using var processedBmp = ResizeIfTooLarge(originalBitmap, 2048);
                using var ms = new MemoryStream();
                processedBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                base64Image = Convert.ToBase64String(ms.ToArray());
            }
            catch (Exception ex)
            {
                SafeLogger.Log(ex, "Failed to process image for AI explanation.");
                return "画像処理に失敗しました。";
            }

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = "このUIやエラーメッセージ、あるいは画面に表示されている内容について、文脈を推測して日本語で分かりやすく解説してください。専門用語があればそれも説明してください。" },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = "image/png",
                                    data = base64Image
                                }
                            }
                        }
                    }
                }
            };

            string jsonBody = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-goog-api-key", apiKey);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30)); // 30s timeout

                using var response = await _httpClient.SendAsync(request, cts.Token);
                
                string responseString = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    SafeLogger.Log($"[Gemini Error] HTTP {response.StatusCode}: {responseString}");
                    return $"APIエラーが発生しました (HTTP {(int)response.StatusCode})。\nキーが間違っているか、モデル名が誤っている可能性があります。";
                }

                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;

                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0 &&
                        parts[0].TryGetProperty("text", out var text))
                    {
                        return text.GetString() ?? "解説を取得できませんでした。";
                    }
                    
                    if (firstCandidate.TryGetProperty("finishReason", out var finishReason))
                    {
                        string reason = finishReason.GetString() ?? "";
                        if (reason == "SAFETY") return "安全上の理由（セーフティフィルター）により、AIが解説をブロックしました。";
                        return $"AIが回答を中断しました (Reason: {reason})";
                    }
                }

                return "AIから有効な回答が返されませんでした。";
            }
            catch (OperationCanceledException)
            {
                // Thrown if cancellationToken is canceled (user closed dialog) or 30s timeout
                if (cancellationToken.IsCancellationRequested)
                {
                    throw; // Let App.xaml.cs handle user cancellation gracefully
                }
                return "APIリクエストがタイムアウトしました (30秒)。";
            }
            catch (Exception ex)
            {
                SafeLogger.Log(ex, "Exception during Gemini API call");
                return $"通信エラーが発生しました: {SafeLogger.Sanitize(ex.Message)}";
            }
        }

        private Bitmap ResizeIfTooLarge(Bitmap src, int maxDimension)
        {
            if (src.Width <= maxDimension && src.Height <= maxDimension)
            {
                return new Bitmap(src); // clone to return independent instance
            }

            float scale = Math.Min((float)maxDimension / src.Width, (float)maxDimension / src.Height);
            int newW = Math.Max(1, (int)(src.Width * scale));
            int newH = Math.Max(1, (int)(src.Height * scale));

            var result = new Bitmap(newW, newH);
            using var g = Graphics.FromImage(result);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, newW, newH);
            return result;
        }
    }
}
