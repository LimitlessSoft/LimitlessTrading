namespace LT.Indicators.Sdk;

/// <summary>
/// The members of <see cref="Indicator"/> as statics, for script files: scripts are compiled with
/// <c>using static LT.Indicators.Sdk.Script;</c> so <c>Close</c>, <c>Input(…)</c> and
/// <c>Plot(…)</c> resolve in an editor too (the generated <c>indicators.csproj</c> has the same
/// usings), and in helper classes declared in a script. They act on the indicator computing on
/// the current thread.
/// </summary>
public static class Script
{
	public static string? Name
	{
		get => Indicator.Current.Name;
		set => Indicator.Current.Name = value;
	}

	public static string? ShortName
	{
		get => Indicator.Current.ShortName;
		set => Indicator.Current.ShortName = value;
	}

	public static PaneKind Pane
	{
		get => Indicator.Current.Pane;
		set => Indicator.Current.Pane = value;
	}

	public static double? ScaleMin
	{
		get => Indicator.Current.ScaleMin;
		set => Indicator.Current.ScaleMin = value;
	}

	public static double? ScaleMax
	{
		get => Indicator.Current.ScaleMax;
		set => Indicator.Current.ScaleMax = value;
	}

	public static int? Decimals
	{
		get => Indicator.Current.Decimals;
		set => Indicator.Current.Decimals = value;
	}

	public static int BarCount => Indicator.Current.Bars.Count;
	public static Series Open => Indicator.Current.GetOpen();
	public static Series High => Indicator.Current.GetHigh();
	public static Series Low => Indicator.Current.GetLow();
	public static Series Close => Indicator.Current.GetClose();
	public static Series Volume => Indicator.Current.GetVolume();
	public static Series Hl2 => (High + Low) / 2;
	public static Series Hlc3 => (High + Low + Close) / 3;
	public static Series Ohlc4 => (Open + High + Low + Close) / 4;
	public static IReadOnlyList<DateTimeOffset> Time => Indicator.Current.Bars.Time;
	public static bool IsIntraday => Indicator.Current.Bars.IsIntraday;
	public static TimeZoneInfo SessionTimeZone => Indicator.Current.Bars.SessionTimeZone;
	public static Series NewSession => Indicator.Current.GetNewSession();

	public static int Input(
		string name,
		int defaultValue,
		int min = int.MinValue,
		int max = int.MaxValue
	) => Indicator.Current.GetInt(name, defaultValue, min, max);

	public static double Input(
		string name,
		double defaultValue,
		double min = double.MinValue,
		double max = double.MaxValue,
		double step = 0
	) => Indicator.Current.GetDouble(name, defaultValue, min, max, step);

	public static bool Input(string name, bool defaultValue) =>
		Indicator.Current.GetBool(name, defaultValue);

	public static void Plot(
		string name,
		Series series,
		Color? color = null,
		double width = 1.5,
		LineStyle style = LineStyle.Solid
	) => Indicator.Current.AddLine(name, series, color, width, style);

	public static void Histogram(
		string name,
		Series series,
		Color? color = null,
		Color? negativeColor = null
	) => Indicator.Current.AddHistogram(name, series, color, negativeColor);

	public static void ColoredHistogram(
		string name,
		Series series,
		IReadOnlyList<Color> colors,
		Color? color = null
	) => Indicator.Current.AddColoredHistogram(name, series, colors, color);

	public static void Band(
		string name,
		Series upper,
		Series lower,
		Color? color = null,
		double fillOpacity = 0.12
	) => Indicator.Current.AddBand(name, upper, lower, color, fillOpacity);

	public static void Markers(
		string name,
		Series condition,
		MarkerShape shape = MarkerShape.TriangleUp,
		Color? color = null,
		MarkerPosition position = MarkerPosition.BelowBar,
		Series? at = null
	) => Indicator.Current.AddMarkers(name, condition, shape, color, position, at);

	public static void Level(
		string name,
		double value,
		Color? color = null,
		LineStyle style = LineStyle.Dashed
	) => Indicator.Current.AddLevel(name, value, color, style);
}
