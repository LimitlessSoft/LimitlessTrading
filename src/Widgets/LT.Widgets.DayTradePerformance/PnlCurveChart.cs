using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using LT.Contracts.Dtos.Performance;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.DayTradePerformance;

/// <summary>
/// Intraday P&amp;L curve over time: green above zero, red below, zero line, value axis on the right
/// and a readout of the point under the cursor (the last one otherwise). Same look as the
/// candlestick chart.
/// </summary>
public class PnlCurveChart : Control
{
	public static readonly StyledProperty<IReadOnlyList<PnlSampleDto>?> SamplesProperty =
		AvaloniaProperty.Register<PnlCurveChart, IReadOnlyList<PnlSampleDto>?>(nameof(Samples));

	public static readonly StyledProperty<TimeZoneInfo?> TimeZoneProperty =
		AvaloniaProperty.Register<PnlCurveChart, TimeZoneInfo?>(nameof(TimeZone));

	private const double ValueAxisWidth = 72;
	private const double TimeAxisHeight = 22;
	private const double TopPadding = 24;

	private static readonly ImmutableSolidColorBrush Background = new(Color.Parse("#15181D"));
	private static readonly ImmutableSolidColorBrush UpBrush = new(Color.Parse("#26A69A"));
	private static readonly ImmutableSolidColorBrush DownBrush = new(Color.Parse("#EF5350"));
	private static readonly ImmutableSolidColorBrush UpFill = new(Color.Parse("#3326A69A"));
	private static readonly ImmutableSolidColorBrush DownFill = new(Color.Parse("#33EF5350"));
	private static readonly ImmutableSolidColorBrush TextBrush = new(Color.Parse("#8A93A3"));
	private static readonly IPen GridPen = new ImmutablePen(
		new ImmutableSolidColorBrush(Color.Parse("#222731")),
		1
	);
	private static readonly IPen ZeroPen = new ImmutablePen(
		new ImmutableSolidColorBrush(Color.Parse("#6B7385")),
		1
	);
	private static readonly IPen CrosshairPen = new ImmutablePen(
		new ImmutableSolidColorBrush(Color.Parse("#6B7385")),
		1,
		new ImmutableDashStyle([3, 3], 0)
	);
	private static readonly IPen UpPen = new ImmutablePen(UpBrush, 1.5);
	private static readonly IPen DownPen = new ImmutablePen(DownBrush, 1.5);

	private Point? _pointer;

	static PnlCurveChart()
	{
		AffectsRender<PnlCurveChart>(SamplesProperty, TimeZoneProperty);
		ClipToBoundsProperty.OverrideDefaultValue<PnlCurveChart>(true);
	}

	public IReadOnlyList<PnlSampleDto>? Samples
	{
		get => GetValue(SamplesProperty);
		set => SetValue(SamplesProperty, value);
	}

	/// <summary>Zone of the time axis and the readout (the display zone); null is the local zone.</summary>
	public TimeZoneInfo? TimeZone
	{
		get => GetValue(TimeZoneProperty);
		set => SetValue(TimeZoneProperty, value);
	}

	public override void Render(DrawingContext context)
	{
		var size = Bounds.Size;
		context.DrawRectangle(Background, null, new Rect(size));

		var samples = Samples;
		if (samples is not { Count: > 0 } || size.Width < ValueAxisWidth + 20 || size.Height < 60)
		{
			var empty = Text("No P&L recorded for this day", 13, TextBrush);
			context.DrawText(
				empty,
				new Point((size.Width - empty.Width) / 2, (size.Height - empty.Height) / 2)
			);
			return;
		}

		var plot = new Rect(
			0,
			TopPadding,
			size.Width - ValueAxisWidth,
			size.Height - TopPadding - TimeAxisHeight
		);

		// Zero is always in range, so the curve reads as above / below flat
		var low = Math.Min(0, samples.Min(x => x.Value));
		var high = Math.Max(0, samples.Max(x => x.Value));
		if (high - low < 1)
		{
			high += 0.5;
			low -= 0.5;
		}
		var padding = (high - low) * 0.08;
		low -= padding;
		high += padding;

		var start = samples[0].Time;
		var span = Math.Max(60, (samples[^1].Time - start).TotalSeconds);

		double Y(double value) => plot.Bottom - (value - low) / (high - low) * plot.Height;
		double X(DateTimeOffset time) => plot.X + (time - start).TotalSeconds / span * plot.Width;

		// value grid + axis labels
		var step = NiceStep((high - low) / Math.Max(2, plot.Height / 40));
		for (var value = Math.Ceiling(low / step) * step; value <= high; value += step)
		{
			var y = Math.Round(Y(value)) + 0.5;
			context.DrawLine(GridPen, new Point(plot.X, y), new Point(plot.Right, y));
			var label = Text(value.ToString("#,0.##", CultureInfo.CurrentCulture), 11, TextBrush);
			context.DrawText(label, new Point(plot.Right + 6, y - label.Height / 2));
		}

		// time labels, about every 110 px
		var labels = Math.Max(1, (int)(plot.Width / 110));
		for (var i = 0; i <= labels; i++)
		{
			var time = start.AddSeconds(span * i / labels);
			var x = Math.Round(X(time)) + 0.5;
			var label = Text(ToShown(time).ToString("HH:mm"), 11, TextBrush);
			var left = Math.Clamp(x - label.Width / 2, 0, plot.Right - label.Width);
			context.DrawText(label, new Point(left, plot.Bottom + 4));
		}

		var zeroY = Math.Round(Y(0)) + 0.5;
		context.DrawLine(ZeroPen, new Point(plot.X, zeroY), new Point(plot.Right, zeroY));

		// area to the zero line and the line itself, clipped once above and once below zero
		var line = new StreamGeometry();
		using (var g = line.Open())
		{
			g.BeginFigure(new Point(X(samples[0].Time), Y(samples[0].Value)), false);
			foreach (var sample in samples.Skip(1))
				g.LineTo(new Point(X(sample.Time), Y(sample.Value)));
			if (samples.Count == 1)
				g.LineTo(new Point(plot.Right, Y(samples[0].Value)));
			g.EndFigure(false);
		}
		var area = new StreamGeometry();
		using (var g = area.Open())
		{
			var lastX = samples.Count == 1 ? plot.Right : X(samples[^1].Time);
			g.BeginFigure(new Point(X(samples[0].Time), zeroY), true);
			foreach (var sample in samples)
				g.LineTo(new Point(X(sample.Time), Y(sample.Value)));
			if (samples.Count == 1)
				g.LineTo(new Point(lastX, Y(samples[0].Value)));
			g.LineTo(new Point(lastX, zeroY));
			g.EndFigure(true);
		}
		using (context.PushClip(new Rect(plot.X, plot.Y, plot.Width, zeroY - plot.Y)))
		{
			context.DrawGeometry(UpFill, null, area);
			context.DrawGeometry(null, UpPen, line);
		}
		using (context.PushClip(new Rect(plot.X, zeroY, plot.Width, plot.Bottom - zeroY)))
		{
			context.DrawGeometry(DownFill, null, area);
			context.DrawGeometry(null, DownPen, line);
		}

		// crosshair + readout of the nearest point
		var shown = samples[^1];
		if (_pointer is { } p && plot.Contains(p))
		{
			var time = start.AddSeconds((p.X - plot.X) / plot.Width * span);
			shown = samples.MinBy(x => Math.Abs((x.Time - time).TotalSeconds))!;
			var x = Math.Round(X(shown.Time)) + 0.5;
			context.DrawLine(CrosshairPen, new Point(x, plot.Y), new Point(x, plot.Bottom));
		}
		var readout = Text(
			$"{ToShown(shown.Time):HH:mm:ss}   P&L {shown.Value.ToString("+#,0.00;-#,0.00;0.00", CultureInfo.CurrentCulture)}",
			12,
			shown.Value >= 0 ? UpBrush : DownBrush
		);
		context.DrawText(readout, new Point(6, 4));
	}

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

	private DateTimeOffset ToShown(DateTimeOffset time) =>
		DisplayTimeHelpers.ToZone(time, TimeZone ?? TimeZoneInfo.Local);

	private static FormattedText Text(string text, double size, IBrush brush) =>
		new(
			text,
			CultureInfo.CurrentCulture,
			FlowDirection.LeftToRight,
			Typeface.Default,
			size,
			brush
		);

	/// <summary>1, 2 or 5 times a power of ten, at least <paramref name="raw"/>.</summary>
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
}
