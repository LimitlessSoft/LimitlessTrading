using System.Globalization;

namespace LT.Indicators.Sdk;

/// <summary>An sRGB colour with alpha. <see cref="Colors"/> has named ones.</summary>
public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
{
	/// <summary>"#RRGGBB" or "#RRGGBBAA" (the leading # is optional).</summary>
	public static Color FromHex(string hex)
	{
		ArgumentNullException.ThrowIfNull(hex);
		var text = hex.StartsWith('#') ? hex[1..] : hex;
		if (
			(text.Length != 6 && text.Length != 8)
			|| !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)
		)
			throw new FormatException($"'{hex}' is not a #RRGGBB or #RRGGBBAA colour.");
		return text.Length == 6
			? new Color((byte)(v >> 16), (byte)(v >> 8), (byte)v)
			: new Color((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
	}

	public static Color FromRgb(byte r, byte g, byte b) => new(r, g, b);

	/// <summary>The same colour with <paramref name="opacity"/> (0 … 1) as its alpha.</summary>
	public Color WithOpacity(double opacity) =>
		this with
		{
			A = (byte)Math.Round(Math.Clamp(double.IsNaN(opacity) ? 1 : opacity, 0, 1) * 255),
		};

	/// <summary>0xAARRGGBB.</summary>
	public uint ToArgb() => (uint)(A << 24 | R << 16 | G << 8 | B);

	public override string ToString() => $"#{R:X2}{G:X2}{B:X2}{A:X2}";
}
