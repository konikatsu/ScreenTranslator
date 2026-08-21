using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Tesseract;

namespace ScreenTranslator.Services;

public class OcrService : IDisposable
{
    private TesseractEngine? _tesseractEngine;
    private readonly string _tessdataPath;

    public OcrService()
    {
        // Check tessdata in base directory or app directory
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _tessdataPath = Path.Combine(baseDir, "tessdata");

        if (!Directory.Exists(_tessdataPath))
        {
            // Fallback check in current working directory
            string cwdTessdata = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
            if (Directory.Exists(cwdTessdata))
            {
                _tessdataPath = cwdTessdata;
            }
        }

        try
        {
            if (Directory.Exists(_tessdataPath))
            {
                _tesseractEngine = new TesseractEngine(_tessdataPath, "eng", EngineMode.Default);
                _tesseractEngine.DefaultPageSegMode = PageSegMode.SparseText;
            }
        }
        catch
        {
            _tesseractEngine = null;
        }
    }

    public Task<string> RecognizeTextAsync(Bitmap originalBitmap)
    {
        return Task.Run(() =>
        {
            try
            {
                // Preprocess bitmap: 2.5x upscale with Bicubic interpolation for sharper UI text recognition
                using var processedBitmap = PreprocessImage(originalBitmap);

                if (_tesseractEngine != null)
                {
                    using var ms = new MemoryStream();
                    processedBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    byte[] imageBytes = ms.ToArray();

                    using var pix = Pix.LoadFromMemory(imageBytes);
                    using var page = _tesseractEngine.Process(pix);

                    string text = page.GetText();
                    string cleaned = CleanOcrOutput(text);

                    if (!string.IsNullOrWhiteSpace(cleaned))
                    {
                        return cleaned;
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                return $"OCR Error: {ex.Message}";
            }
        });
    }

    private Bitmap PreprocessImage(Bitmap src)
    {
        // Upscale factor
        float scale = 2.5f;
        int newWidth = (int)(src.Width * scale);
        int newHeight = (int)(src.Height * scale);

        var result = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            g.DrawImage(src, 0, 0, newWidth, newHeight);
        }

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
            // Filter out single character noise from icons
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
