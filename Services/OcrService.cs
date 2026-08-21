using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Globalization;

namespace ScreenTranslator.Services;

public class OcrService
{
    private OcrEngine? _ocrEngine;

    public OcrService()
    {
        InitializeEngine();
    }

    private void InitializeEngine()
    {
        // Try English first, then fallback to user profile languages
        var englishLang = new Language("en-US");
        if (OcrEngine.IsLanguageSupported(englishLang))
        {
            _ocrEngine = OcrEngine.TryCreateFromLanguage(englishLang);
        }

        _ocrEngine ??= OcrEngine.TryCreateFromUserProfileLanguages();
    }

    public async Task<string> RecognizeTextAsync(Bitmap bitmap)
    {
        if (_ocrEngine == null)
        {
            return string.Empty;
        }

        try
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Bmp);
            ms.Position = 0;

            var randomAccessStream = ms.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied
            );

            var ocrResult = await _ocrEngine.RecognizeAsync(softwareBitmap);

            var sb = new StringBuilder();
            foreach (var line in ocrResult.Lines)
            {
                sb.AppendLine(line.Text);
            }

            return sb.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"OCR Error: {ex.Message}";
        }
    }
}
