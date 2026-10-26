using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using LT.Contracts.Dtos.AccountPerformance;
using LT.Contracts.Enums;

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

	// Cash movement badges: a colour and a glyph per kind, apart from the P&L green / red
	public static readonly ImmutableSolidColorBrush DepositBrush = new ImmutableSolidColorBrush(
		Color.Parse("#3FB950")
	);
	public static readonly ImmutableSolidColorBrush WithdrawalBrush = new ImmutableSolidColorBrush(
		Color.Parse("#F0883E")
	);
	public static readonly ImmutableSolidColorBrush TransferBrush = new ImmutableSolidColorBrush(
		Color.Parse("#A371F7")
	);
	private static readonly IPen BadgeRingPen = new ImmutablePen(Background, 1.5);
	private static readonly IPen GlyphPen = new ImmutablePen(
		new ImmutableSolidColorBrush(Colors.White),
		1.6,
		lineCap: PenLineCap.Round
	);

	/// <summary>Height of one movement badge on a chart, spacing included.</summary>
	public const double BadgeSlot = 14;

	public static IBrush GetBrush(CashMovementKind kind) =>
		kind switch
		{
			CashMovementKind.Deposit => DepositBrush,
			CashMovementKind.Withdrawal => WithdrawalBrush,
			_ => TransferBrush,
		};

	/// <summary>
	/// A round badge centred on <paramref name="center"/>: deposit a green +, withdrawal an orange
	/// −, transfer in / out a purple arrow down (into the account) / up (out of it).
	/// </summary>
	public static void DrawCashMovementBadge(
		DrawingContext context,
		Point center,
		CashMovementKind kind,
		double radius
	)
	{
		context.DrawEllipse(GetBrush(kind), BadgeRingPen, center, radius, radius);
		var r = radius * 0.5;
		var (x, y) = (center.X, center.Y);
		switch (kind)
		{
			case CashMovementKind.Deposit:
				context.DrawLine(GlyphPen, new Point(x - r, y), new Point(x + r, y));
				context.DrawLine(GlyphPen, new Point(x, y - r), new Point(x, y + r));
				break;
			case CashMovementKind.Withdrawal:
				context.DrawLine(GlyphPen, new Point(x - r, y), new Point(x + r, y));
				break;
			default:
				var dir = kind == CashMovementKind.TransferIn ? 1 : -1;
				context.DrawLine(GlyphPen, new Point(x, y - r * dir), new Point(x, y + r * dir));
				context.DrawLine(
					GlyphPen,
					new Point(x - r * 0.75, y + r * dir * 0.25),
					new Point(x, y + r * dir)
				);
				context.DrawLine(
					GlyphPen,
					new Point(x + r * 0.75, y + r * dir * 0.25),
					new Point(x, y + r * dir)
				);
				break;
		}
	}

	/// <summary>
	/// Badges for the movements of each chart point, stacked up from the bottom of
	/// <paramref name="plot"/> at the point's x (at most three; the readout lists all).
	/// </summary>
	public static void DrawCashMovementBadges(
		DrawingContext context,
		Rect plot,
		IReadOnlyDictionary<int, IReadOnlyList<CashMovementDto>> byPoint,
		Func<int, double> x
	)
	{
		foreach (var (index, movements) in byPoint)
			for (var i = 0; i < Math.Min(3, movements.Count); i++)
				DrawCashMovementBadge(
					context,
					new Point(x(index), plot.Bottom - BadgeSlot / 2 - 1 - i * BadgeSlot),
					movements[i].Kind,
					BadgeSlot / 2 - 1
				);
	}

	/// <summary>The hovered point's movements after its readout, in the first one's colour.</summary>
	public static void DrawCashMovementReadout(
		DrawingContext context,
		FormattedText readout,
		IReadOnlyList<CashMovementDto>? movements
	)
	{
		if (movements is not { Count: > 0 })
			return;
		var text = Text(
			"   " + string.Join(",  ", movements.Select(m => CashMovementText.Short(m))),
			12,
			GetBrush(movements[0].Kind)
		);
		context.DrawText(text, new Point(8 + readout.Width, 3));
	}

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
