using System.Collections.Concurrent;
using SkiaSharp;

namespace Bussiness
{
    /// <summary>
    /// Cross-platform replacement for the GDI+ (System.Drawing) text rendering used by
    /// CheckCode and WindMgr. System.Drawing.Common is Windows-only on .NET 6+.
    /// </summary>
    public static class SkiaText
    {
        private static readonly ConcurrentDictionary<string, SKTypeface> typefaces = new ConcurrentDictionary<string, SKTypeface>();

        public static SKTypeface GetTypeface(string family, SKFontStyle style)
        {
            string key = family + "|" + style.Weight + "|" + style.Slant;
            return typefaces.GetOrAdd(key, _ => SKTypeface.FromFamilyName(family, style) ?? SKTypeface.Default);
        }

        public static SKColor ToSk(System.Drawing.Color c)
        {
            return new SKColor(c.R, c.G, c.B, c.A);
        }

        /// <summary>GDI+ font sizes are in points; convert to pixels at 96 DPI.</summary>
        public static float PointsToPixels(float pt)
        {
            return pt * 96f / 72f;
        }

        /// <summary>
        /// Equivalent of Graphics.DrawString(text, font, brush, x, y, format) with
        /// Alignment = LineAlignment = StringAlignment.Center (text centered on the point).
        /// </summary>
        public static void DrawCentered(SKCanvas canvas, string text, SKTypeface typeface, float sizePt, SKColor color, float x, float y)
        {
            using (SKPaint paint = new SKPaint
            {
                Typeface = typeface,
                TextSize = PointsToPixels(sizePt),
                Color = color,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            })
            {
                SKFontMetrics m = paint.FontMetrics;
                float baseline = y - (m.Ascent + m.Descent) / 2f;
                canvas.DrawText(text, x, baseline, paint);
            }
        }

        public static byte[] EncodePng(SKBitmap bitmap)
        {
            using (SKImage image = SKImage.FromBitmap(bitmap))
            using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
            {
                return data.ToArray();
            }
        }
    }
}
