using System.Drawing;
using SkiaSharp;
using System.IO;

namespace Bussiness
{
    public class CheckCode
    {
        private enum RandomStringMode
		{
			LowerLetter,
            UpperLetter,
            Letter,
            Digital,
            Mix
		}

        private static Color[] c = new Color[2]
		{
			Color.Gray,
			Color.DimGray
		};

        private static char[] digitals = new char[]
		{
			'1',
			'2',
			'3',
			'4',
			'5',
			'6',
			'7',
			'8',
			'9',
			'0'
		};

        private static string[] font = new string[5]
		{
			"Verdana",
			"Terminal",
			"Comic Sans MS",
			"Arial",
			"Tekton Pro"
		};

        private static char[] letters = new char[50]
		{
			'a',
			'b',
			'c',
			'd',
			'e',
			'f',
			'g',
			'h',
			'i',
			'j',
			'k',
			'l',
			'm',
			'n',
			'p',
			'q',
			'r',
			's',
			't',
			'u',
			'v',
			'w',
			'x',
			'y',
			'z',
			'A',
			'B',
			'C',
			'D',
			'E',
			'F',
			'G',
			'H',
			'I',
			'J',
			'K',
			'L',
			'M',
			'N',
			'P',
			'Q',
			'R',
			'S',
			'T',
			'U',
			'V',
			'W',
			'X',
			'Y',
			'Z'
		};

        private static char[] lowerLetters = new char[21]
		{
			'a',
			'b',
			'c',
			'd',
			'e',
			'f',
			'h',
			'k',
			'm',
			'n',
			'p',
			'q',
			'r',
			's',
			't',
			'u',
			'v',
			'w',
			'x',
			'y',
			'z'
		};

        private static char[] mix = new char[51]
		{
			'2',
			'3',
			'4',
			'5',
			'6',
			'7',
			'8',
			'9',
			'a',
			'b',
			'c',
			'd',
			'e',
			'f',
			'h',
			'k',
			'm',
			'n',
			'p',
			'q',
			'r',
			's',
			't',
			'u',
			'v',
			'w',
			'x',
			'y',
			'z',
			'A',
			'B',
			'C',
			'D',
			'E',
			'F',
			'G',
			'H',
			'K',
			'M',
			'N',
			'P',
			'Q',
			'R',
			'S',
			'T',
			'U',
			'V',
			'W',
			'X',
			'Y',
			'Z'
		};

        public static ThreadSafeRandom rand = new ThreadSafeRandom();

        private static char[] upperLetters = new char[22]
		{
			'A',
			'B',
			'C',
			'D',
			'E',
			'F',
			'G',
			'H',
			'K',
			'M',
			'N',
			'P',
			'Q',
			'R',
			'S',
			'T',
			'U',
			'V',
			'W',
			'X',
			'Y',
			'Z'
		};

        public static byte[] CreateImage(string randomcode)
        {
			int maxValue = 30;
			int width = randomcode.Length * 30;
			int height = 32;
			using (SKBitmap image = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul))
			using (SKCanvas graphics = new SKCanvas(image))
			{
				graphics.Clear(SKColors.Transparent);
				int index = rand.Next(2);
				SKColor color = SkiaText.ToSk(c[index]);
				for (int i = 0; i < 1; i++)
				{
					int num11 = rand.Next(width / 2);
					int num12 = rand.Next(width * 3 / 4, width);
					int num13 = rand.Next(height);
					int num14 = rand.Next(height);
					using (SKPath path = new SKPath())
					using (SKPaint pen = new SKPaint { Color = color, StrokeWidth = 2f, IsStroke = true, IsAntialias = true })
					{
						path.MoveTo(num11, num13);
						path.CubicTo((num11 + num12) / 4, 0f, (num11 + num12) * 3 / 4, height, num12, num14);
						graphics.DrawPath(path, pen);
					}
				}
				char[] chArray = randomcode.ToCharArray();
				for (int j = 0; j < chArray.Length; j++)
				{
					int num10 = rand.Next(5);
					SKTypeface typeface = SkiaText.GetTypeface(CheckCode.font[num10], SKFontStyle.Bold);
					float angle = ThreadSafeRandom.NextStatic(-maxValue, maxValue);
					graphics.Translate(16f, 16f);
					graphics.RotateDegrees(angle);
					SkiaText.DrawCentered(graphics, chArray[j].ToString(), typeface, 22f, color, 1f, 1f);
					graphics.RotateDegrees(0f - angle);
					graphics.Translate(2f, -16f);
				}
				graphics.Flush();
				return SkiaText.EncodePng(image);
			}
        }

        public static string GenerateCheckCode()
        {
			return GenerateRandomString(4, RandomStringMode.Digital);
        }

        private static string GenerateRandomString(int length, RandomStringMode mode)
        {
			string str = string.Empty;
			if (length != 0)
			{
				switch (mode)
				{
				case RandomStringMode.LowerLetter:
				{
					for (int j = 0; j < length; j++)
					{
						str += lowerLetters[rand.Next(0, lowerLetters.Length)];
					}
					return str;
				}
				case RandomStringMode.UpperLetter:
				{
					for (int k = 0; k < length; k++)
					{
						str += upperLetters[rand.Next(0, upperLetters.Length)];
					}
					return str;
				}
				case RandomStringMode.Letter:
				{
					for (int l = 0; l < length; l++)
					{
						str += letters[rand.Next(0, letters.Length)];
					}
					return str;
				}
				case RandomStringMode.Digital:
				{
					for (int m = 0; m < length; m++)
					{
						str += digitals[rand.Next(0, digitals.Length)];
					}
					return str;
				}
				}
				for (int i = 0; i < length; i++)
				{
					str += mix[rand.Next(0, mix.Length)];
				}
			}
			return str;
        }
    }
}
