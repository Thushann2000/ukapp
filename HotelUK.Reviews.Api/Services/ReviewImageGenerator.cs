using HotelUK.Reviews.Api.Models;
using SkiaSharp;

namespace HotelUK.Reviews.Api.Services;

/// <summary>
/// Draws the square Instagram graphic for one review, entirely in memory.
/// Brand palette is shared with the web page so the two look like one family.
/// </summary>
public sealed class ReviewImageGenerator
{
    // ---- Brand palette (same hex values as the web page) --------------------
    private static readonly SKColor LagoonDeep = SKColor.Parse("#04212A");
    private static readonly SKColor LagoonMid = SKColor.Parse("#0E6A78");
    private static readonly SKColor Shallow = SKColor.Parse("#3FBFBF");
    private static readonly SKColor Shell = SKColor.Parse("#FCF8F2");
    private static readonly SKColor SunriseGold = SKColor.Parse("#E3A62F");

    private const int Size = 1080;
    private const int Margin = 96;

    private readonly SKTypeface _display;
    private readonly SKTypeface _displayItalic;
    private readonly SKTypeface _body;
    private readonly ILogger<ReviewImageGenerator> _logger;

    public ReviewImageGenerator(ILogger<ReviewImageGenerator> logger, IWebHostEnvironment env)
    {
        _logger = logger;

        var fontDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts");

        // Same two families as the web page, so the graphic and the page look
        // like one piece of work. Instrument Serif has no bold cut, which is
        // exactly how the headings are set on the page too.
        _display = LoadTypeface(fontDir, "InstrumentSerif-Regular.ttf", "Georgia", SKFontStyle.Normal);
        _displayItalic = LoadTypeface(fontDir, "InstrumentSerif-Italic.ttf", "Georgia", SKFontStyle.Italic);
        _body = LoadTypeface(fontDir, "InstrumentSans-Regular.ttf", "DejaVu Sans", SKFontStyle.Normal);
    }

    /// <summary>Renders the review as a PNG and returns the raw bytes.</summary>
    public byte[] Render(ReviewSubmission review)
    {
        var info = new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        DrawBackground(canvas);
        DrawFrame(canvas);

        DrawLogoBadge(canvas, Size / 2f, 210f, 76f);

        using (var wordmark = new SKFont(_body, 30f))
        {
            DrawTracked(canvas, "HOTEL UK PASSIKUDAH", Size / 2f, 330f,
                        wordmark, Shell.WithAlpha(230), tracking: 7.5f);
        }

        DrawStars(canvas, Size / 2f, 400f, review.Rating, radius: 24f, gap: 20f);

        DrawReviewBody(canvas, review.ReviewText, top: 470f, bottom: 810f);

        DrawSignature(canvas, review);
        DrawWaves(canvas);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 95);
        return data.ToArray();
    }

    // ------------------------------------------------------------------ parts

    private static void DrawBackground(SKCanvas canvas)
    {
        using var bg = new SKPaint { IsAntialias = true };
        bg.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, Size),
            new[] { LagoonMid, LagoonDeep },
            new[] { 0f, 1f },
            SKShaderTileMode.Clamp);
        canvas.DrawRect(new SKRect(0, 0, Size, Size), bg);

        // Soft turquoise light coming from the top-right, like sun on the bay.
        using var glow = new SKPaint { IsAntialias = true };
        glow.Shader = SKShader.CreateRadialGradient(
            new SKPoint(Size * 0.86f, Size * 0.10f),
            Size * 0.62f,
            new[] { Shallow.WithAlpha(64), Shallow.WithAlpha(0) },
            new[] { 0f, 1f },
            SKShaderTileMode.Clamp);
        canvas.DrawRect(new SKRect(0, 0, Size, Size), glow);
    }

    private static void DrawFrame(SKCanvas canvas)
    {
        using var stroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            Color = Shallow.WithAlpha(80)
        };
        var r = new SKRect(Margin * 0.62f, Margin * 0.62f, Size - Margin * 0.62f, Size - Margin * 0.62f);
        canvas.DrawRoundRect(r, 28f, 28f, stroke);
    }

    /// <summary>The hotel mark: a ring, a rising sun, "UK", and the waterline.</summary>
    private void DrawLogoBadge(SKCanvas canvas, float cx, float cy, float radius)
    {
        using var ring = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            Color = SunriseGold
        };
        canvas.DrawCircle(cx, cy, radius, ring);

        var waterline = cy + radius * 0.30f;

        // Rising sun: a half disc sitting on the waterline.
        using var sun = new SKPaint { IsAntialias = true, Color = SunriseGold };
        using var sunPath = new SKPath();
        var sunR = radius * 0.40f;
        sunPath.AddArc(new SKRect(cx - sunR, waterline - sunR, cx + sunR, waterline + sunR), 180, 180);
        sunPath.Close();
        canvas.DrawPath(sunPath, sun);

        // Waterline + one ripple below it.
        using var line = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            StrokeCap = SKStrokeCap.Round,
            Color = Shallow
        };
        canvas.DrawLine(cx - radius * 0.58f, waterline, cx + radius * 0.58f, waterline, line);
        line.StrokeWidth = 2f;
        line.Color = Shallow.WithAlpha(160);
        canvas.DrawLine(cx - radius * 0.34f, waterline + radius * 0.24f,
                        cx + radius * 0.34f, waterline + radius * 0.24f, line);

        // "UK" above the sun.
        using var monogram = new SKFont(_display, radius * 0.52f);
        using var text = new SKPaint { IsAntialias = true, Color = Shell };
        canvas.DrawText("UK", cx, waterline - sunR - radius * 0.16f, SKTextAlign.Center, monogram, text);
    }

    private static void DrawStars(SKCanvas canvas, float cx, float cy, int rating, float radius, float gap)
    {
        rating = Math.Clamp(rating, 1, 5);
        var step = radius * 2 + gap;
        var startX = cx - (step * 4) / 2f;

        using var filled = new SKPaint { IsAntialias = true, Color = SunriseGold };
        using var empty = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.5f,
            Color = Shell.WithAlpha(90)
        };

        for (var i = 0; i < 5; i++)
        {
            using var star = BuildStar(startX + step * i, cy, radius, radius * 0.44f);
            canvas.DrawPath(star, i < rating ? filled : empty);
        }
    }

    private static SKPath BuildStar(float cx, float cy, float outerR, float innerR)
    {
        var path = new SKPath();
        for (var i = 0; i < 10; i++)
        {
            var r = i % 2 == 0 ? outerR : innerR;
            var angle = -MathF.PI / 2 + i * MathF.PI / 5;
            var x = cx + r * MathF.Cos(angle);
            var y = cy + r * MathF.Sin(angle);
            if (i == 0) path.MoveTo(x, y); else path.LineTo(x, y);
        }
        path.Close();
        return path;
    }

    /// <summary>
    /// Centres the review text in the space available, shrinking the type until
    /// it fits. Long reviews are trimmed with an ellipsis rather than overflowing.
    /// </summary>
    private void DrawReviewBody(SKCanvas canvas, string reviewText, float top, float bottom)
    {
        var clean = CollapseWhitespace(reviewText);
        var typeface = TypefaceFor(clean);
        var maxWidth = Size - (Margin * 2);
        var maxHeight = bottom - top;

        SKFont font = null!;
        List<string> lines = null!;
        float lineHeight = 0;

        for (var size = 54f; size >= 26f; size -= 2f)
        {
            font?.Dispose();
            font = new SKFont(typeface, size);
            lineHeight = size * 1.42f;
            lines = WrapText(clean, font, maxWidth);
            if (lines.Count * lineHeight <= maxHeight) break;
        }

        // Still too long even at the smallest size: cut it down.
        var maxLines = (int)(maxHeight / lineHeight);
        if (lines.Count > maxLines && maxLines > 0)
        {
            lines = lines.Take(maxLines).ToList();
            lines[^1] = lines[^1].TrimEnd() + "…";
        }

        using var quote = new SKPaint { IsAntialias = true, Color = Shallow.WithAlpha(90) };
        using var quoteFont = new SKFont(_display, 130f);
        canvas.DrawText("\u201C", Size / 2f, top - 18f, SKTextAlign.Center, quoteFont, quote);

        using var paint = new SKPaint { IsAntialias = true, Color = Shell };
        var blockHeight = lines.Count * lineHeight;
        var y = top + (maxHeight - blockHeight) / 2f + lineHeight * 0.78f;

        foreach (var line in lines)
        {
            canvas.DrawText(line, Size / 2f, y, SKTextAlign.Center, font, paint);
            y += lineHeight;
        }

        font.Dispose();
    }

    private void DrawSignature(SKCanvas canvas, ReviewSubmission review)
    {
        using var rule = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = SunriseGold.WithAlpha(150)
        };
        canvas.DrawLine(Size / 2f - 44f, 852f, Size / 2f + 44f, 852f, rule);

        using var nameFont = new SKFont(_display, 40f);
        using var namePaint = new SKPaint { IsAntialias = true, Color = SunriseGold };
        canvas.DrawText(Truncate(review.CustomerName.Trim(), 34), Size / 2f, 908f,
                        SKTextAlign.Center, nameFont, namePaint);

        // Not "verified guest": the form is open to anyone, and saying otherwise
        // on a public post is a claim the hotel cannot back up.
        var footer = string.IsNullOrWhiteSpace(review.Country)
            ? "GUEST REVIEW  ·  PASIKUDA, SRI LANKA"
            : $"GUEST REVIEW  ·  {review.Country!.Trim().ToUpperInvariant()}";

        using var footerFont = new SKFont(_body, 22f);
        DrawTracked(canvas, footer, Size / 2f, 956f, footerFont,
                    Shell.WithAlpha(150), tracking: 4f);
    }

    private static void DrawWaves(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            Color = Shallow.WithAlpha(55)
        };

        for (var band = 0; band < 3; band++)
        {
            using var path = new SKPath();
            var baseY = 1006f + band * 20f;
            path.MoveTo(-20f, baseY);
            for (var x = -20f; x <= Size + 20f; x += 60f)
            {
                path.CubicTo(x + 15f, baseY - 9f, x + 45f, baseY + 9f, x + 60f, baseY);
            }
            canvas.DrawPath(path, paint);
        }
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>SkiaSharp has no letter-spacing, so tracked text is drawn glyph by glyph.</summary>
    private static void DrawTracked(SKCanvas canvas, string text, float centreX, float y,
                                    SKFont font, SKColor color, float tracking)
    {
        using var paint = new SKPaint { IsAntialias = true, Color = color };

        var total = 0f;
        foreach (var ch in text) total += font.MeasureText(ch.ToString()) + tracking;
        total -= tracking;

        var x = centreX - total / 2f;
        foreach (var ch in text)
        {
            var s = ch.ToString();
            canvas.DrawText(s, x, y, SKTextAlign.Left, font, paint);
            x += font.MeasureText(s) + tracking;
        }
    }

    private static List<string> WrapText(string text, SKFont font, float maxWidth)
    {
        var lines = new List<string>();
        var current = "";

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (font.MeasureText(candidate) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0) { lines.Add(current); current = ""; }

            // A single word wider than the line - a URL, or a language that does
            // not use spaces. Break it by character rather than letting it spill.
            if (font.MeasureText(word) > maxWidth)
            {
                foreach (var ch in word)
                {
                    var grown = current + ch;
                    if (font.MeasureText(grown) > maxWidth && current.Length > 0)
                    {
                        lines.Add(current);
                        current = ch.ToString();
                    }
                    else
                    {
                        current = grown;
                    }
                }
            }
            else
            {
                current = word;
            }
        }

        if (current.Length > 0) lines.Add(current);
        return lines;
    }

    /// <summary>
    /// Instrument Serif covers Latin. A review written in Russian, Sinhala or
    /// Tamil would come out as empty boxes, so for those we hand the whole block
    /// to whatever the container has installed instead.
    /// </summary>
    private SKTypeface TypefaceFor(string text)
    {
        var foreign = 0;
        char sample = ' ';

        foreach (var ch in text)
        {
            if (!char.IsLetter(ch) || IsLatinRange(ch)) continue;
            if (foreign == 0) sample = ch;
            foreign++;
            if (foreign >= 3) break;
        }

        if (foreign < 3) return _displayItalic;

        try
        {
            var fallback = SKFontManager.Default.MatchCharacter(sample);
            if (fallback is not null)
            {
                _logger.LogInformation("Review is not in a Latin script; drawing it with {Family}.",
                                       fallback.FamilyName);
                return fallback;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No fallback typeface found for U+{Code:X4}.", (int)sample);
        }

        return _displayItalic;
    }

    private static bool IsLatinRange(char ch) =>
        ch <= 0x024F                       // Latin, Latin-1, Latin Extended A and B
        || (ch >= 0x1E00 && ch <= 0x1EFF); // Latin Extended Additional (Vietnamese)

    /// <summary>
    /// Squashes runs of whitespace and drops anything outside the basic plane -
    /// emoji, mostly. No font in the container can draw them, so they would come
    /// out as empty boxes. The full text with the emoji still goes in the
    /// Facebook and Instagram captions; only the drawn picture leaves them out.
    /// </summary>
    private static string CollapseWhitespace(string input)
    {
        var builder = new System.Text.StringBuilder(input.Length);

        foreach (var ch in input)
        {
            if (char.IsSurrogate(ch)) continue;                 // emoji and the like
            if (ch == '\uFE0F' || ch == '\uFE0E' || ch == '\u200D') continue; // emoji joiners
            builder.Append(ch);
        }

        return string.Join(' ', builder.ToString()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";

    private SKTypeface LoadTypeface(string fontDir, string fileName, string fallbackFamily, SKFontStyle style)
    {
        var path = Path.Combine(fontDir, fileName);
        if (File.Exists(path))
        {
            var fromFile = SKTypeface.FromFile(path);
            if (fromFile is not null) return fromFile;
        }

        _logger.LogWarning("Font {File} not found in {Dir}; falling back to {Family}.",
                           fileName, fontDir, fallbackFamily);

        return SKTypeface.FromFamilyName(fallbackFamily, style)
               ?? SKTypeface.FromFamilyName(null, style)
               ?? SKTypeface.Default;
    }
}
