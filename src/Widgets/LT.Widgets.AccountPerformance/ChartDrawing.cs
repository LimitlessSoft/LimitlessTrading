using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace LT.App.Widgets.AccountPerformance;

/// <summary>Colours and text shared by the performance charts (the candlestick chart's palette).</summary>
internal static class ChartDrawing
{
	public const double ValueAxisWidth = 72;
	public const double DateAxisHeight = 20;
	public const double TopPadding = 22;

	public static readonly ImmutableSolidColorBrush Background = new ImmutableSolidColorBrush(
		Color.Parse("#15181D")
	);
	public static readonly ImmutableSolidColorBrush UpBrush = new ImmutableSolidColorBrush(
		Color.Parse("#26A69A")
	);
	public static readonly ImmutableSolidColorBrush DownBrush = new ImmutableSolidColorBrush(
		Color.Parse("#EF5350")
	);
	public static readonly ImmutableSolidColorBrush TextBrush = new ImmutableSolidColorBrush(
		Color.Parse("#8A93A3")
	);
	public static readonly ImmutableSolidColorBrush LabelBrush = new ImmutableSolidColorBrush(
		Color.Parse("#E6E8EB")
	);
	public static readonly ImmutableSolidColorBrush LineBrush = new ImmutableSolidColorBrush(
		Color.Parse("#4C8DFF")
	);
	public static readonly ImmutableSolidColorBrush AreaBrush = new ImmutableSolidColorBrush(
		Color.Parse("#334C8DFF")
	);
	public static readonly IPen GridPen = new ImmutablePen(
		new ImmutableSolidColorBrush(Color.Parse("#222731")),
		1
	);
	public static readonly IPen ZeroPen = new ImmutablePen(
		new ImmutableSolidColorBrush(Color.Parse("#3A4150")),
		1
	);
	public static readonly IPen CrosshairPen = new ImmutablePen(
		new ImmutableSolidColorBrush(Color.Parse("#6B7385")),
		1,
		new ImmutableDashStyle([3, 3], 0)
	);
	public static readonly IPen LinePen = new ImmutablePen(LineBrush, 1.5);

	public static FormattedText Text(string text, double size, IBrush brush) =>
		new(
			text,
			CultureInfo.CurrentCulture,
			FlowDirection.LeftToRight,
			Typeface.Default,
			size,
			brush
		);

	/// <summary>1, 2 or 5 times a power of ten, at least <paramref name="raw"/>.</summary>
	public static double NiceStep(double raw)
	{
		if (raw <= 0 || double.IsNaN(raw))
			return 1;
		var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
		var normalized = raw / magnitude;
		return (
				normalized <= 1 ? 1
				: normalized <= 2 ? 2
				: normalized <= 5 ? 5
				: 10
			) * magnitude;
	}

	/// <summary>Grid lines with value labels on the right axis, from <paramref name="low"/> to <paramref name="high"/>.</summary>
	public static void DrawValueGrid(
		DrawingContext context,
		Rect plot,
		double low,
		double high,
		Func<double, double> y
	)
	{
		var step = NiceStep((high - low) / Math.Max(2, plot.Height / 40));
		for (var value = Math.Ceiling(low / step) * step; value <= high; value += step)
		{
			var lineY = Math.Round(y(value)) + 0.5;
			context.DrawLine(GridPen, new Point(plot.X, lineY), new Point(plot.Right, lineY));
			var label = Text(CompactNumber(value), 11, TextBrush);
			context.DrawText(label, new Point(plot.Right + 6, lineY - label.Height / 2));
		}
	}

	/// <summary>Axis label: 950, 12.5K, 1.25M.</summary>
	public static string CompactNumber(double value)
	{
		var abs = Math.Abs(value);
		return abs switch
		{
			>= 1_000_000 => (value / 1_000_000).ToString("0.##", CultureInfo.CurrentCulture) + "M",
			>= 10_000 => (value / 1_000).ToString("0.#", CultureInfo.CurrentCulture) + "K",
			_ => value.ToString("#,0.##", CultureInfo.CurrentCulture),
		};
	}

	public static string Date(DateOnly date) => date.ToString("dd MMM", CultureInfo.CurrentCulture);
}
