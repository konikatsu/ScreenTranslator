using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
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
            // Try English first
            var englishLang = new Language("en-US");
            if (OcrEngine.IsLanguageSupported(englishLang))
            {
                _winOcrEngine = OcrEngine.TryCreateFromLanguage(englishLang);
            }

            // Fallback to Japanese (which can still partially read Latin characters)
            if (_winOcrEngine == null)
            {
                var jaLang = new Language("ja");
                if (OcrEngine.IsLanguageSupported(jaLang))
                {
                    _winOcrEngine = OcrEngine.TryCreateFromLanguage(jaLang);
                }
            }

            // Final fallback
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
            // For single-file publish, AppDomain.BaseDirectory points to temp extraction dir.
            // The tessdata folder is alongside the actual .exe on disk.
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
        // Strategy: Try Tesseract (English-specialized) first, fall back to Windows OCR

        // 1. Try Tesseract (high accuracy for English)
        string tesseractResult = await TryTesseractAsync(originalBitmap);
        if (!string.IsNullOrWhiteSpace(tesseractResult) && tesseractResult.Length >= 3)
        {
            return tesseractResult;
        }

        // 2. Fallback: Windows OCR (works even with Japanese engine for Latin text)
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
            // Upscale for better Windows OCR recognition
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

    private Bitmap PreprocessImage(Bitmap src)
    {
        float scale = 3.0f;
        int newWidth = (int)(src.Width * scale);
        int newHeight = (int)(src.Height * scale);

        // Step 1: Upscale with high quality interpolation
        var upscaled = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(upscaled))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.DrawImage(src, 0, 0, newWidth, newHeight);
        }

        // Step 2: Convert to grayscale and auto-detect dark/light mode
        long totalBrightness = 0;
        var grayValues = new byte[newWidth * newHeight];

        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                Color pixel = upscaled.GetPixel(x, y);
                int gray = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
                grayValues[y * newWidth + x] = (byte)gray;
                totalBrightness += gray;
            }
        }

        long totalPixels = (long)newWidth * newHeight;
        double avgBrightness = totalBrightness / (double)totalPixels;
        bool isDarkMode = avgBrightness < 128;

        // Step 3: Create output - grayscale, inverted if dark mode (OCR needs dark text on light bg)
        var result = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);
        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                int gray = grayValues[y * newWidth + x];
                if (isDarkMode)
                {
                    gray = 255 - gray; // Invert: light text on dark bg → dark text on light bg
                }
                result.SetPixel(x, y, Color.FromArgb(255, gray, gray, gray));
            }
        }

        upscaled.Dispose();
        return result;
    }

    private string CleanOcrOutput(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();

        foreach (var line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.Length > 1 || (trimmed.Length == 1 && char.IsLetterOrDigit(trimmed[0])))
            {
                sb.AppendLine(trimmed);
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
