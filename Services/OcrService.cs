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

    public OcrService()
    {
        InitializeWindowsOcr();
        InitializeTesseract();
    }

    private void InitializeWindowsOcr()
    {
        try
        {
            var englishLang = new Language("en-US");
            if (OcrEngine.IsLanguageSupported(englishLang))
            {
                _winOcrEngine = OcrEngine.TryCreateFromLanguage(englishLang);
            }

            if (_winOcrEngine == null)
            {
                var jaLang = new Language("ja");
                if (OcrEngine.IsLanguageSupported(jaLang))
                {
                    _winOcrEngine = OcrEngine.TryCreateFromLanguage(jaLang);
                }
            }

            _winOcrEngine ??= OcrEngine.TryCreateFromUserProfileLanguages();
        }
        catch
        {
            _winOcrEngine = null;
        }
    }

    private void InitializeTesseract()
    {
        try
        {
            string? exePath = Environment.ProcessPath;
            string? exeDir = exePath != null ? Path.GetDirectoryName(exePath) : null;

            string[] searchPaths = new[]
            {
                exeDir != null ? Path.Combine(exeDir, "tessdata") : "",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata"),
                Path.Combine(Directory.GetCurrentDirectory(), "tessdata"),
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
                _tesseractEngine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default);
                _tesseractEngine.DefaultPageSegMode = PageSegMode.SparseText;
            }
        }
        catch
        {
            _tesseractEngine = null;
        }
    }

    public async Task<string> RecognizeTextAsync(Bitmap originalBitmap)
    {
        // 1. Try Tesseract (English-specialized) first
        string tesseractResult = await TryTesseractAsync(originalBitmap);
        if (!string.IsNullOrWhiteSpace(tesseractResult) && tesseractResult.Length >= 2)
        {
            return tesseractResult;
        }

        // 2. Fallback to Windows OCR
        string winOcrResult = await TryWindowsOcrAsync(originalBitmap);
        if (!string.IsNullOrWhiteSpace(winOcrResult))
        {
            return winOcrResult;
        }

        return string.Empty;
    }

    private Task<string> TryTesseractAsync(Bitmap originalBitmap)
    {
        if (_tesseractEngine == null) return Task.FromResult(string.Empty);

        return Task.Run(() =>
        {
            try
            {
                using var processedBitmap = PreprocessImage(originalBitmap);
                using var ms = new MemoryStream();
                processedBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                byte[] imageBytes = ms.ToArray();

                using var pix = Pix.LoadFromMemory(imageBytes);
                using var page = _tesseractEngine.Process(pix);

                string text = page.GetText();
                return CleanOcrOutput(text);
            }
            catch
            {
                return string.Empty;
            }
        });
    }

    private async Task<string> TryWindowsOcrAsync(Bitmap bitmap)
    {
        if (_winOcrEngine == null) return string.Empty;

        try
        {
            using var processedBitmap = PreprocessImage(bitmap);
            using var ms = new MemoryStream();
            processedBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            ms.Position = 0;

            var randomAccessStream = ms.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied
            );

            var ocrResult = await _winOcrEngine.RecognizeAsync(softwareBitmap);

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

            // Fix common OCR noise in English text (e.g. accidental stray Hiragana/noise characters)
            cleaned = Regex.Replace(cleaned, @"[ぁ-んァ-ヶ]", " "); // Remove stray Japanese characters in English OCR
            cleaned = Regex.Replace(cleaned, @"\s+", " "); // Normalize multiple spaces
            cleaned = Regex.Replace(cleaned, @"\bofyo\b", "of your", RegexOptions.IgnoreCase); // Common OCR fix
            cleaned = Regex.Replace(cleaned, @"\byo\b", "your", RegexOptions.IgnoreCase);

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
