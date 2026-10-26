namespace LT.Indicators.Sdk;

/// <summary>Named colours that read well on the chart's dark background.</summary>
public static class Colors
{
	public static Color Blue => Color.FromHex("#2962FF");
	public static Color Teal => Color.FromHex("#26A69A");
	public static Color Green => Color.FromHex("#4CAF50");
	public static Color Lime => Color.FromHex("#C0CA33");
	public static Color Yellow => Color.FromHex("#FDD835");
	public static Color Orange => Color.FromHex("#FF9800");
	public static Color Red => Color.FromHex("#EF5350");
	public static Color Pink => Color.FromHex("#EC407A");
	public static Color Purple => Color.FromHex("#7E57C2");
	public static Color Aqua => Color.FromHex("#00BCD4");
	public static Color Navy => Color.FromHex("#3F51B5");
	public static Color Gray => Color.FromHex("#787B86");
	public static Color Silver => Color.FromHex("#B2B5BE");
	public static Color White => Color.FromHex("#FFFFFF");

	/// <summary>Plots without a colour of their own take these in turn.</summary>
	public static IReadOnlyList<Color> Palette { get; } =
	[Blue, Orange, Purple, Teal, Pink, Yellow, Aqua, Lime, Red, Green];
}
