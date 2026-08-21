using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tesseract;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Globalization;

namespace ScreenTranslator.Services;

public class OcrService : IDisposable
{
    private TesseractEngine? _tesseractEngine;
    private OcrEngine? _winOcrEngine;
    private string _initLog = "";

    public OcrService()
    {
        InitializeTesseract();
        InitializeWindowsOcr();
    }

    private void InitializeTesseract()
    {
        try
        {
            string? exePath = Environment.ProcessPath;
            string? exeDir = exePath != null ? Path.GetDirectoryName(exePath) : null;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string curDir = Directory.GetCurrentDirectory();

            string[] searchPaths = new[]
            {
                exeDir != null ? Path.Combine(exeDir, "tessdata") : "",
                Path.Combine(baseDir, "tessdata"),
                Path.Combine(curDir, "tessdata"),
                @"C:\dev\ScreenTranslator\tessdata",
                @"C:\dev\ScreenTranslator\publish\tessdata"
            };

            string? tessdataPath = null;
            foreach (var path in searchPaths)
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path)
                    && File.Exists(Path.Combine(path, "eng.traineddata")))
                {
                    tessdataPath = path;
                    break;
                }
            }

            if (tessdataPath != null)
            {
                // Ensure native x64 directory is in PATH so tesseract50.dll can be loaded
                string? nativeDir = exeDir != null ? Path.Combine(exeDir, "x64") : null;
                if (nativeDir != null && Directory.Exists(nativeDir))
                {
                    string currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                    if (!currentPath.Contains(nativeDir))
                    {
                        Environment.SetEnvironmentVariable("PATH", nativeDir + ";" + currentPath);
                    }
                }

                _tesseractEngine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default);
                _tesseractEngine.DefaultPageSegMode = PageSegMode.Auto; // Auto is best for full paragraphs and multi-line UI
                _initLog += $"[Tesseract OK: {tessdataPath}] ";
            }
            else
            {
                _initLog += "[Tesseract Fail: tessdata not found] ";
            }
        }
        catch (Exception ex)
        {
            _initLog += $"[Tesseract Exception: {ex.Message}] ";
            _tesseractEngine = null;
        }
    }

    private void InitializeWindowsOcr()
    {
        try
        {
            // ONLY use English Windows OCR. NEVER fallback to Japanese OCR for English UI!
            var englishLang = new Language("en-US");
            if (OcrEngine.IsLanguageSupported(englishLang))
            {
                _winOcrEngine = OcrEngine.TryCreateFromLanguage(englishLang);
                _initLog += "[WinOCR en-US OK]";
            }
            else
            {
                var enGb = new Language("en-GB");
                if (OcrEngine.IsLanguageSupported(enGb))
                {
                    _winOcrEngine = OcrEngine.TryCreateFromLanguage(enGb);
                    _initLog += "[WinOCR en-GB OK]";
                }
                else
                {
                    _winOcrEngine = null;
                    _initLog += "[WinOCR: No English pack]";
                }
            }
        }
        catch (Exception ex)
        {
            _initLog += $"[WinOCR Exception: {ex.Message}]";
            _winOcrEngine = null;
        }
    }

    public async Task<string> RecognizeTextAsync(Bitmap originalBitmap)
    {
        // 1. Try Tesseract first
        if (_tesseractEngine != null)
        {
            string tesseractResult = await TryTesseractAsync(originalBitmap);
            if (!string.IsNullOrWhiteSpace(tesseractResult) && tesseractResult.Length >= 2)
            {
                return tesseractResult;
            }
        }

        // 2. Fallback to Windows OCR (English only)
        if (_winOcrEngine != null)
        {
            string winOcrResult = await TryWindowsOcrAsync(originalBitmap);
            if (!string.IsNullOrWhiteSpace(winOcrResult))
            {
                return winOcrResult;
            }
        }

        if (_tesseractEngine == null && _winOcrEngine == null)
        {
            return $"[OCR Engine Not Available]\n{_initLog}";
        }

        return string.Empty;
    }

    private Task<string> TryTesseractAsync(Bitmap originalBitmap)
    {
        return Task.Run(() =>
        {
            try
            {
                using var processedBitmap = PreprocessImage(originalBitmap);
                using var ms = new MemoryStream();
                processedBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                byte[] imageBytes = ms.ToArray();

                using var pix = Pix.LoadFromMemory(imageBytes);
                using var page = _tesseractEngine!.Process(pix);

                string text = page.GetText();
                return CleanOcrOutput(text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Tesseract Error] {ex.Message}");
                return string.Empty;
            }
        });
    }

    private Task<string> TryWindowsOcrAsync(Bitmap bitmap)
    {
        return Task.Run(async () =>
        {
            try
            {
                using var processedBitmap = PreprocessImage(bitmap);
                using var ms = new MemoryStream();
                processedBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                ms.Position = 0;

                using var randomAccessStream = ms.AsRandomAccessStream();
                var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
                using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied
                );

                var ocrResult = await _winOcrEngine!.RecognizeAsync(softwareBitmap);

                var sb = new StringBuilder();
                foreach (var line in ocrResult.Lines)
                {
                    sb.AppendLine(line.Text);
                }

                return CleanOcrOutput(sb.ToString());
            }
            catch
            {
                return string.Empty;
            }
        });
    }

    /// <summary>
    /// Advanced image preprocessing with 3x upscaling, contrast auto-stretching, 
    /// and dark/light mode inversion. Ensures faint gray subtitle text becomes crisp solid black.
    /// </summary>
    private Bitmap PreprocessImage(Bitmap src)
    {
        float scale = 3.0f;
        int newWidth = Math.Max(1, (int)(src.Width * scale));
        int newHeight = Math.Max(1, (int)(src.Height * scale));

        // Step 1: Upscale using high quality bicubic interpolation
        var upscaled = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(upscaled))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.DrawImage(src, 0, 0, newWidth, newHeight);
        }

        // Step 2: LockBits memory processing
        var rect = new Rectangle(0, 0, newWidth, newHeight);
        var bmpData = upscaled.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int totalBytes = Math.Abs(bmpData.Stride) * newHeight;
        byte[] rgbValues = new byte[totalBytes];

        Marshal.Copy(bmpData.Scan0, rgbValues, 0, totalBytes);

        // Pass 1: Compute min, max, and average brightness
        int minGray = 255;
        int maxGray = 0;
        long totalBrightness = 0;
        int pixelCount = newWidth * newHeight;

        for (int i = 0; i < totalBytes; i += 4)
        {
            byte b = rgbValues[i];
            byte g = rgbValues[i + 1];
            byte r = rgbValues[i + 2];
            int gray = (int)(0.299 * r + 0.587 * g + 0.114 * b);
            
            if (gray < minGray) minGray = gray;
            if (gray > maxGray) maxGray = gray;
            totalBrightness += gray;
        }

        bool isDarkMode = (totalBrightness / (double)pixelCount) < 128;
        int contrastRange = Math.Max(1, maxGray - minGray);

        // Pass 2: Apply Auto Contrast Stretching and Inversion
        for (int i = 0; i < totalBytes; i += 4)
        {
            byte b = rgbValues[i];
            byte g = rgbValues[i + 1];
            byte r = rgbValues[i + 2];
            int gray = (int)(0.299 * r + 0.587 * g + 0.114 * b);

            // Stretch dynamic range to 0..255
            int stretched = ((gray - minGray) * 255) / contrastRange;

            if (isDarkMode)
            {
                // Dark mode: light text -> dark text (0), dark bg -> white bg (255)
                stretched = 255 - stretched;
            }

            // High-contrast curve for crisp letter edges (darken text below threshold)
            if (stretched < 180)
            {
                stretched = (int)(stretched * 0.7); // Darken text
            }
            else
            {
                stretched = Math.Min(255, (int)(stretched * 1.15)); // Whiten background
            }

            byte finalVal = (byte)Math.Clamp(stretched, 0, 255);
            rgbValues[i] = finalVal;     // B
            rgbValues[i + 1] = finalVal; // G
            rgbValues[i + 2] = finalVal; // R
        }

        Marshal.Copy(rgbValues, 0, bmpData.Scan0, totalBytes);
        upscaled.UnlockBits(bmpData);

        return upscaled;
    }

    private string CleanOcrOutput(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();

        foreach (var line in lines)
        {
            string cleaned = line.Trim();

            // Fix common OCR noise in English text
            cleaned = Regex.Replace(cleaned, @"[ぁ-んァ-ヶ一-龠]", " "); // Remove any stray Japanese characters in English OCR
            cleaned = Regex.Replace(cleaned, @"\s+", " "); // Normalize multiple spaces
            cleaned = Regex.Replace(cleaned, @"\bofyo\b", "of your", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\blirnit\b", "limit", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\b5-hourlirnit\b", "5-hour limit", RegexOptions.IgnoreCase);

            if (cleaned.Length > 1 || (cleaned.Length == 1 && char.IsLetterOrDigit(cleaned[0])))
            {
                sb.AppendLine(cleaned);
            }
        }

        return sb.ToString().Trim();
    }

    public void Dispose()
    {
        _tesseractEngine?.Dispose();
        _tesseractEngine = null;
    }
}
