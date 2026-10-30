using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using LT.App.Helpers;
using LT.Contracts.Dtos.GammaExposure;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.GammaExposure;

/// <summary>
/// One column per strike: the call exposure as a faint green bar up, the put exposure as a faint
/// red bar down and the net as a solid bar in front (green above zero, red below). The spot price
/// is a blue line, the gamma flip a dashed orange one, each placed between the strikes around it.
/// Hover shows the strike under the cursor.
/// </summary>
public class GammaExposureChart : Control
{
	public static readonly StyledProperty<GammaExposureDto?> ExposureProperty =
		AvaloniaProperty.Register<GammaExposureChart, GammaExposureDto?>(nameof(Exposure));

	private const double ValueAxisWidth = 64;
	private const double StrikeAxisHeight = 20;
	private const double TopPadding = 22;
	private const double MaxBarWidth = 26;

	private static readonly ImmutableSolidColorBrush Background = new(Color.Parse("#15181D"));
	private static readonly ImmutableSolidColorBrush UpBrush = new(Color.Parse("#26A69A"));
	private static readonly ImmutableSolidColorBrush DownBrush = new(Color.Parse("#EF5350"));
	private static readonly ImmutableSolidColorBrush CallBrush = new(Color.Parse("#4026A69A"));
	private static readonly ImmutableSolidColorBrush PutBrush = new(Color.Parse("#40EF5350"));
	private static readonly ImmutableSolidColorBrush TextBrush = new(Color.Parse("#8A93A3"));
	private static readonly ImmutableSolidColorBrush SpotBrush = new(Color.Parse("#4C8DFF"));
	private static readonly ImmutableSolidColorBrush FlipBrush = new(Color.Parse("#F0883E"));
	private static readonly ImmutableSolidColorBrush HoverBrush = new(Color.Parse("#14FFFFFF"));
	private static readonly IPen GridPen = new ImmutablePen(
		new ImmutableSolidColorBrush(Color.Parse("#222731")),
		1
	);
	private static readonly IPen ZeroPen = new ImmutablePen(
		new ImmutableSolidColorBrush(Color.Parse("#3A4150")),
		1
	);
	private static readonly IPen SpotPen = new ImmutablePen(SpotBrush, 1.5);
	private static readonly IPen FlipPen = new ImmutablePen(
		FlipBrush,
		1.5,
		new ImmutableDashStyle([4, 3], 0)
	);

	private Point? _pointer;

	static GammaExposureChart()
	{
		AffectsRender<GammaExposureChart>(ExposureProperty);
		ClipToBoundsProperty.OverrideDefaultValue<GammaExposureChart>(true);
	}

	public GammaExposureDto? Exposure
	{
		get => GetValue(ExposureProperty);
		set => SetValue(ExposureProperty, value);
	}

	public override void Render(DrawingContext context)
	{
		var size = Bounds.Size;
		context.DrawRectangle(Background, null, new Rect(size));

		var strikes = Exposure?.Strikes ?? [];
		if (strikes.Count == 0 || size.Width < ValueAxisWidth + 40 || size.Height < 80)
			return;

		var plot = new Rect(
			8,
			TopPadding,
			size.Width - ValueAxisWidth - 8,
			size.Height - TopPadding - StrikeAxisHeight
		);
		var high = Math.Max(0, strikes.Max(x => Math.Max(x.CallExposure, x.NetExposure)));
		var low = Math.Min(0, strikes.Min(x => Math.Min(x.PutExposure, x.NetExposure)));
		if (high - low <= 0)
		{
			high = 1;
			low = -1;
		}
		var padding = (high - low) * 0.06;
		high += padding;
		low -= padding;

		var slot = plot.Width / strikes.Count;
		var barWidth = Math.Max(1, Math.Min(MaxBarWidth, slot * 0.75));
		double X(int index) => plot.X + (index + 0.5) * slot;
		double Y(double value) => plot.Bottom - (value - low) / (high - low) * plot.Height;

		var hovered =
			_pointer is { } p && plot.Contains(p)
				? Math.Clamp((int)((p.X - plot.X) / slot), 0, strikes.Count - 1)
				: (int?)null;
		if (hovered is { } h)
			context.DrawRectangle(
				HoverBrush,
				null,
				new Rect(X(h) - slot / 2, plot.Y, slot, plot.Height)
			);

		DrawValueGrid(context, plot, low, high, Y);
		var zeroY = Math.Round(Y(0)) + 0.5;
		context.DrawLine(ZeroPen, new Point(plot.X, zeroY), new Point(plot.Right, zeroY));

		var labelEvery = Math.Max(1, (int)Math.Ceiling(44 / slot));
		for (var i = 0; i < strikes.Count; i++)
		{
			var strike = strikes[i];
			DrawBar(context, X(i), barWidth, Y(strike.CallExposure), Y(0), CallBrush);
			DrawBar(context, X(i), barWidth, Y(0), Y(strike.PutExposure), PutBrush);
			var net = strike.NetExposure;
			DrawBar(
				context,
				X(i),
				Math.Max(1, barWidth * 0.5),
				Y(Math.Max(0, net)),
				Y(Math.Min(0, net)),
				net >= 0 ? UpBrush : DownBrush
			);
			if (i % labelEvery != 0)
				continue;
			var label = Text(FormatStrike(strike.Strike), 11, TextBrush);
			var x = Math.Clamp(X(i) - label.Width / 2, plot.X, plot.Right - label.Width);
			context.DrawText(label, new Point(x, plot.Bottom + 3));
		}

		var exposure = Exposure!;
		DrawPriceLine(context, plot, strikes, X, exposure.GammaFlip, FlipPen, FlipBrush, "Flip");
		DrawPriceLine(context, plot, strikes, X, exposure.SpotPrice, SpotPen, SpotBrush, "Spot");

		var readout = hovered is { } index
			? Text(
				$"{FormatStrike(strikes[index].Strike)}   Net {GammaExposureHelpers.FormatExposure(strikes[index].NetExposure)}"
					+ $"   Calls {GammaExposureHelpers.FormatExposure(strikes[index].CallExposure)} (OI {FormatHelpers.Quantity(strikes[index].CallOpenInterest)})"
					+ $"   Puts {GammaExposureHelpers.FormatExposure(strikes[index].PutExposure)} (OI {FormatHelpers.Quantity(strikes[index].PutOpenInterest)})",
				12,
				strikes[index].NetExposure >= 0 ? UpBrush : DownBrush
			)
			: Text("Gamma exposure per 1% move, by strike", 12, TextBrush);
		context.DrawText(readout, new Point(8, 3));
	}

	private static void DrawBar(
		DrawingContext context,
		double centerX,
		double width,
		double top,
		double bottom,
		IBrush brush
	)
	{
		if (bottom - top < 0.5)
			return;
		context.DrawRectangle(brush, null, new Rect(centerX - width / 2, top, width, bottom - top));
	}

	// A vertical line at the price, placed between the two strikes around it; none outside them
	private static void DrawPriceLine(
		DrawingContext context,
		Rect plot,
		IReadOnlyList<GammaExposureStrikeDto> strikes,
		Func<int, double> x,
		double? price,
		IPen pen,
		IBrush brush,
		string name
	)
	{
		if (price is not { } value || GetX(strikes, x, value) is not { } lineX)
			return;
		lineX = Math.Round(lineX) + 0.5;
		context.DrawLine(pen, new Point(lineX, plot.Y), new Point(lineX, plot.Bottom));
		var label = Text($"{name} {FormatHelpers.Price(value)}", 11, brush);
		var labelX = Math.Clamp(lineX + 4, plot.X, plot.Right - label.Width);
		context.DrawText(label, new Point(labelX, plot.Y + (name == "Spot" ? 2 : 16)));
	}

	private static double? GetX(
		IReadOnlyList<GammaExposureStrikeDto> strikes,
		Func<int, double> x,
		double price
	)
	{
		if (strikes.Count == 1)
			return Math.Abs(strikes[0].Strike - price) < 1e-9 ? x(0) : null;
		for (var i = 0; i < strikes.Count - 1; i++)
		{
			var (from, to) = (strikes[i].Strike, strikes[i + 1].Strike);
			if (price < from || price > to)
				continue;
			return x(i) + (x(i + 1) - x(i)) * (to > from ? (price - from) / (to - from) : 0);
		}
		return null;
	}

	private static void DrawValueGrid(
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
			var label = Text(GammaExposureHelpers.FormatExposure(value), 11, TextBrush);
			context.DrawText(label, new Point(plot.Right + 6, lineY - label.Height / 2));
		}
	}

	// 1, 2 or 5 times a power of ten, at least raw
	private static double NiceStep(double raw)
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

	private static string FormatStrike(double strike) =>
		strike.ToString("0.##", CultureInfo.CurrentCulture);

	private static FormattedText Text(string text, double size, IBrush brush) =>
		new(
			text,
			CultureInfo.CurrentCulture,
			FlowDirection.LeftToRight,
			Typeface.Default,
			size,
			brush
		);

	protected override void OnPointerMoved(PointerEventArgs e)
	{
		base.OnPointerMoved(e);
		_pointer = e.GetPosition(this);
		InvalidateVisual();
	}

	protected override void OnPointerExited(PointerEventArgs e)
	{
		base.OnPointerExited(e);
		_pointer = null;
		InvalidateVisual();
	}
}
