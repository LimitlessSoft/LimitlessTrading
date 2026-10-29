using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

/// <summary>
/// Footprint bars (<see cref="BarStyle"/> = <see cref="ChartBarStyle.Footprint"/>): each bar's
/// rows of volume at the bid (left) × at the ask (right), shaded grey by volume, diagonal
/// imbalances red (sellers) and blue (buyers), the bar's point of control boxed, with a thin
/// candle down the middle; a volume profile of the visible footprints along the plot's right
/// edge, its point of control orange. Rows merge several ticks as the view zooms out so they stay
/// readable; numbers show only where every number of a bar fits its cells, shading only where a
/// bar is too narrow for them, and plain candles below that. Bars still loading their trades show
/// the ones in so far with a dotted line over them; bars before their first page are thin candles
/// with three dots above, bars without a footprint candles with a small ring above them. After
/// each render the visible bars are raised (<see cref="VisibleBarsChanged"/>) so their trades
/// load first.
/// </summary>
public partial class CandlestickChart
{
	public static readonly StyledProperty<ChartBarStyle> BarStyleProperty =
		AvaloniaProperty.Register<CandlestickChart, ChartBarStyle>(nameof(BarStyle));

	public static readonly StyledProperty<ChartFootprint?> FootprintProperty =
		AvaloniaProperty.Register<CandlestickChart, ChartFootprint?>(nameof(Footprint));

	public static readonly StyledProperty<int> FootprintVersionProperty = AvaloniaProperty.Register<
		CandlestickChart,
		int
	>(nameof(FootprintVersion));

	public static readonly StyledProperty<double> FootprintImbalanceRatioProperty =
		AvaloniaProperty.Register<CandlestickChart, double>(nameof(FootprintImbalanceRatio), 3);

	// Footprint bars zoom in much further than candles, and open wide enough for their numbers
	private const double FootprintMaxBarSpacing = 320;
	private const double FootprintDefaultBarSpacing = 96;

	// Below this a footprint bar is a plain candle; below the text width only shading
	private const double FootprintMinColumnWidth = 14;
	private const double FootprintTextSize = 10;
	private const double FootprintTextRowHeight = 13;
	private const double FootprintShadeRowHeight = 3;
	private const double FootprintCellPadding = 3;
	private const double VolumeProfileWidthFraction = 0.16;

	private static readonly Color CellColor = Color.Parse("#8A93A3");
	private static readonly Color BidImbalanceColor = Color.Parse("#E53935");
	private static readonly Color AskImbalanceColor = Color.Parse("#1E88E5");
	private static readonly ImmutableSolidColorBrush FootprintUpBrush = new(Color.Parse("#E6E8EB"));
	private static readonly IPen FootprintUpPen = new ImmutablePen(FootprintUpBrush, 1);
	private static readonly IPen PocPen = new ImmutablePen(
		new ImmutableSolidColorBrush(Color.Parse("#FFB300")),
		1.5
	);
	private static readonly ImmutableSolidColorBrush ProfileBrush = new(Color.Parse("#268A93A3"));
	private static readonly ImmutableSolidColorBrush ProfilePocBrush = new(
		Color.Parse("#80FF9800")
	);
	private static readonly IPen PartialPen = new ImmutablePen(
		new ImmutableSolidColorBrush(Color.Parse("#8A93A3")),
		1,
		new ImmutableDashStyle([1, 3], 0)
	);
	private static readonly ImmutableSolidColorBrush LoadingBrush = new(Color.Parse("#8A93A3"));
	private static readonly IPen UnavailablePen = new ImmutablePen(
		new ImmutableSolidColorBrush(Color.Parse("#8A93A3")),
		1.2
	);
	private static readonly IBrush[] CellBrushes = CreateShades(CellColor, 0.10, 0.62);
	private static readonly IBrush[] BidImbalanceBrushes = CreateShades(
		BidImbalanceColor,
		0.55,
		0.95
	);
	private static readonly IBrush[] AskImbalanceBrushes = CreateShades(
		AskImbalanceColor,
		0.55,
		0.95
	);

	// The visible bars last raised (oldest, newest start)
	private (DateTimeOffset From, DateTimeOffset To)? _visibleRaised;

	/// <summary>
	/// The footprint bars shown changed (oldest and newest start): raised after the render, only
	/// with <see cref="BarStyle"/> footprint.
	/// </summary>
	public event Action<DateTimeOffset, DateTimeOffset>? VisibleBarsChanged;

	/// <summary>Candles, or footprint bars when <see cref="Footprint"/> has them.</summary>
	public ChartBarStyle BarStyle
	{
		get => GetValue(BarStyleProperty);
		set => SetValue(BarStyleProperty, value);
	}

	/// <summary>The footprints of the loaded bars; read on the UI thread while rendering.</summary>
	public ChartFootprint? Footprint
	{
		get => GetValue(FootprintProperty);
		set => SetValue(FootprintProperty, value);
	}

	/// <summary>Changes whenever <see cref="Footprint"/> changed inside: redraws.</summary>
	public int FootprintVersion
	{
		get => GetValue(FootprintVersionProperty);
		set => SetValue(FootprintVersionProperty, value);
	}

	/// <summary>Diagonal imbalance ratio (3 = 300 %).</summary>
	public double FootprintImbalanceRatio
	{
		get => GetValue(FootprintImbalanceRatioProperty);
		set => SetValue(FootprintImbalanceRatioProperty, value);
	}

	private bool IsFootprint => BarStyle == ChartBarStyle.Footprint;

	// Footprints load for the bars on screen first: tell the view model which they are
	private void NotifyVisibleBars(IReadOnlyList<BarDto> bars, int start, int end)
	{
		if (!IsFootprint || start >= end)
		{
			_visibleRaised = null;
			return;
		}
		var visible = (bars[start].Time, bars[end - 1].Time);
		if (_visibleRaised == visible)
			return;
		_visibleRaised = visible;
		Dispatcher.UIThread.Post(() => VisibleBarsChanged?.Invoke(visible.Item1, visible.Item2));
	}

	private double GetMaxBarSpacing() => IsFootprint ? FootprintMaxBarSpacing : MaxBarSpacing;

	private double GetDefaultBarSpacing() =>
		IsFootprint ? FootprintDefaultBarSpacing : DefaultBarSpacing;

	// Switching to footprints opens the bars wide enough to read (leaving a fitted period);
	// switching back to candles resets the view
	private void OnFootprintPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		if (change.Property != BarStyleProperty)
			return;
		if (IsFootprint)
		{
			Unfit();
			_barSpacing = FootprintDefaultBarSpacing;
		}
		else
			ResetView();
	}

	/// <summary>What one render needs to draw the visible footprints.</summary>
	private sealed record FootprintFrame(
		ChartFootprint Footprint,
		int TicksPerRow,
		bool ShowsText,
		double ColumnWidth,
		Dictionary<DateTimeOffset, IReadOnlyList<FootprintRowDto>> Rows
	);

	// The visible footprints' rows at this zoom: rows tall enough for text when bars are wide
	// enough for it, thinner shading otherwise; null when the chart draws candles
	private FootprintFrame? GetFootprintFrame(
		IReadOnlyList<BarDto> bars,
		int start,
		int end,
		ChartPane price,
		Rect priceRect
	)
	{
		if (!IsFootprint || Footprint is not { } footprint || priceRect.Height <= 0)
			return null;
		var columnWidth = _barSpacing * 0.92;
		var showsText = columnWidth >= 2 * (FootprintCellPadding * 2 + 12) + 4;
		var pixelsPerTick = Math.Abs(price.Y(0) - price.Y(footprint.TickSize));
		var ticksPerRow = FootprintHelpers.GetTicksPerRow(
			pixelsPerTick,
			showsText ? FootprintTextRowHeight : FootprintShadeRowHeight
		);
		var rows = new Dictionary<DateTimeOffset, IReadOnlyList<FootprintRowDto>>();
		if (columnWidth >= FootprintMinColumnWidth)
			for (var i = start; i < end; i++)
				if (footprint.Get(bars[i].Time) is { } bar)
					rows[bars[i].Time] = FootprintHelpers.GetRows(
						bar,
						ticksPerRow,
						FootprintImbalanceRatio,
						bars[i].Low,
						bars[i].High
					);
		return new FootprintFrame(footprint, ticksPerRow, showsText, columnWidth, rows);
	}

	// One bar: its cells and a thin candle when it has trades and room (a dotted line over a bar
	// still loading the rest); a marked candle before its first trades or without a footprint.
	// False: draw it as a regular candle
	private bool DrawFootprintBar(
		DrawingContext context,
		FootprintFrame frame,
		BarDto bar,
		double x,
		ChartPane price,
		Rect priceRect
	)
	{
		var state = frame.Footprint.GetState(bar.Time);
		var candleWidth = Math.Clamp(frame.ColumnWidth * 0.06, 2, 5);
		if (
			state is FootprintBarState.Ready or FootprintBarState.Partial
			&& frame.Rows.TryGetValue(bar.Time, out var rows)
		)
		{
			DrawFootprintCells(context, frame, rows, x, price, priceRect);
			DrawThinCandle(context, bar, x, price, candleWidth);
			if (state == FootprintBarState.Partial)
			{
				var y = Math.Round(price.Y(bar.High)) - 4.5;
				var half = frame.ColumnWidth / 2 - 1;
				context.DrawLine(PartialPen, new Point(x - half, y), new Point(x + half, y));
			}
			return true;
		}
		if (state == FootprintBarState.Loading && frame.ColumnWidth >= FootprintMinColumnWidth)
		{
			DrawThinCandle(context, bar, x, price, candleWidth);
			var y = price.Y(bar.High) - 6;
			for (var i = -1; i <= 1; i++)
				context.DrawEllipse(LoadingBrush, null, new Point(x + i * 5, y), 1.2, 1.2);
			return true;
		}
		if (state == FootprintBarState.Unavailable && frame.ColumnWidth >= FootprintMinColumnWidth)
		{
			var radius = Math.Clamp(frame.ColumnWidth * 0.08, 2, 4);
			context.DrawEllipse(
				null,
				UnavailablePen,
				new Point(x, price.Y(bar.High) - radius - 4),
				radius,
				radius
			);
		}
		return false;
	}

	private void DrawFootprintCells(
		DrawingContext context,
		FootprintFrame frame,
		IReadOnlyList<FootprintRowDto> rows,
		double x,
		ChartPane price,
		Rect priceRect
	)
	{
		if (rows.Count == 0)
			return;
		var tick = frame.Footprint.TickSize;
		var candleWidth = Math.Clamp(frame.ColumnWidth * 0.06, 2, 5);
		var left = x - frame.ColumnWidth / 2;
		var right = x + frame.ColumnWidth / 2;
		var bidRight = x - candleWidth / 2 - 1;
		var askLeft = x + candleWidth / 2 + 1;
		var cellWidth = bidRight - left;
		var maxCell = rows.Max(r => Math.Max(r.BidVolume, r.AskVolume));

		// Numbers only when every one of this bar fits its cell (never clipped or overlapping)
		List<(FormattedText Bid, FormattedText Ask)>? labels = null;
		if (frame.ShowsText)
		{
			labels = new(rows.Count);
			foreach (var row in rows)
			{
				var bid = Text(
					FootprintHelpers.FormatVolume(row.BidVolume),
					FootprintTextSize,
					LabelBrush
				);
				var ask = Text(
					FootprintHelpers.FormatVolume(row.AskVolume),
					FootprintTextSize,
					LabelBrush
				);
				if (Math.Max(bid.Width, ask.Width) + FootprintCellPadding * 2 > cellWidth)
				{
					labels = null;
					break;
				}
				labels.Add((bid, ask));
			}
		}

		using var clip = context.PushClip(priceRect);
		for (var i = 0; i < rows.Count; i++)
		{
			var row = rows[i];
			var top = price.Y(row.HighPrice + tick / 2);
			var bottom = price.Y(row.Price - tick / 2);
			if (bottom < priceRect.Top || top > priceRect.Bottom)
				continue;
			// A hairline between rows once they are tall enough to tell apart
			var gap = bottom - top >= 6 ? 1 : 0;
			var height = Math.Max(1, bottom - top - gap);
			context.DrawRectangle(
				GetCellBrush(row.BidVolume, maxCell, row.IsBidImbalance, BidImbalanceBrushes),
				null,
				new Rect(left, top, Math.Max(0, bidRight - left), height)
			);
			context.DrawRectangle(
				GetCellBrush(row.AskVolume, maxCell, row.IsAskImbalance, AskImbalanceBrushes),
				null,
				new Rect(askLeft, top, Math.Max(0, right - askLeft), height)
			);
			if (row.IsPoc)
				context.DrawRectangle(
					null,
					PocPen,
					new Rect(
						Math.Round(left) + 0.5,
						Math.Round(top) + 0.5,
						Math.Max(1, Math.Round(right - left) - 1),
						Math.Max(1, Math.Round(height) - 1)
					)
				);
			if (labels != null)
			{
				var (bid, ask) = labels[i];
				var middle = top + height / 2;
				context.DrawText(
					bid,
					new Point(bidRight - FootprintCellPadding - bid.Width, middle - bid.Height / 2)
				);
				context.DrawText(
					ask,
					new Point(askLeft + FootprintCellPadding, middle - ask.Height / 2)
				);
			}
		}
	}

	// White up, red down, narrow enough to leave the cells their room
	private static void DrawThinCandle(
		DrawingContext context,
		BarDto bar,
		double x,
		ChartPane price,
		double width
	)
	{
		var up = bar.Close >= bar.Open;
		var wickX = Math.Round(x) + 0.5;
		context.DrawLine(
			up ? FootprintUpPen : DownPen,
			new Point(wickX, price.Y(bar.High)),
			new Point(wickX, price.Y(bar.Low))
		);
		var top = price.Y(Math.Max(bar.Open, bar.Close));
		var bottom = price.Y(Math.Min(bar.Open, bar.Close));
		context.DrawRectangle(
			up ? FootprintUpBrush : DownBrush,
			null,
			new Rect(x - width / 2, top, width, Math.Max(1, bottom - top))
		);
	}

	// The visible footprints summed by row along the plot's right edge, behind the bars
	private void DrawVolumeProfile(
		DrawingContext context,
		FootprintFrame frame,
		ChartPane price,
		Rect priceRect
	)
	{
		if (frame.Rows.Count == 0)
			return;
		var footprint = frame.Footprint;
		var bars = frame.Rows.Keys.Select(footprint.Get).OfType<FootprintBarDto>();
		var rows = FootprintHelpers.GetRows(
			FootprintHelpers.Combine(bars, footprint.TickSize),
			frame.TicksPerRow,
			FootprintImbalanceRatio
		);
		if (rows.Count == 0)
			return;
		var max = rows.Max(r => r.Volume);
		if (max <= 0)
			return;
		var width = priceRect.Width * VolumeProfileWidthFraction;
		var tick = footprint.TickSize;
		using var clip = context.PushClip(priceRect);
		foreach (var row in rows)
		{
			if (row.Volume <= 0)
				continue;
			var top = price.Y(row.HighPrice + tick / 2);
			var bottom = price.Y(row.Price - tick / 2);
			var height = Math.Max(1, bottom - top - (bottom - top >= 4 ? 1 : 0));
			var length = width * (double)(row.Volume / max);
			context.DrawRectangle(
				row.IsPoc ? ProfilePocBrush : ProfileBrush,
				null,
				new Rect(priceRect.Right - length, top, length, height)
			);
		}
	}

	// The hovered bar's delta and sides, or why it has no footprint
	private string GetFootprintReadout(BarDto bar)
	{
		if (!IsFootprint || Footprint is not { } footprint)
			return string.Empty;
		var partial = footprint.GetLoadedFrom(bar.Time) is { } from
			? $"   (loading: from {ToShown(from).ToString("HH:mm:ss", CultureInfo.CurrentCulture)})"
			: "";
		return footprint.GetState(bar.Time) switch
		{
			FootprintBarState.Ready
			or FootprintBarState.Partial when footprint.Get(bar.Time) is { } fp =>
				$"   Δ {(fp.Delta > 0 ? "+" : "")}{fp.Delta.ToString("#,0.##", CultureInfo.CurrentCulture)}   Bid {fp.BidVolume.ToString("#,0.##", CultureInfo.CurrentCulture)} × Ask {fp.AskVolume.ToString("#,0.##", CultureInfo.CurrentCulture)}{partial}",
			FootprintBarState.Loading => "   footprint loading…",
			FootprintBarState.Unavailable => "   no footprint",
			_ => string.Empty,
		};
	}

	private static IBrush GetCellBrush(
		decimal volume,
		decimal max,
		bool imbalance,
		IBrush[] imbalanceBrushes
	)
	{
		var share = max > 0 ? (double)(volume / max) : 0;
		var shade = (int)Math.Round(share * (CellBrushes.Length - 1));
		return imbalance ? imbalanceBrushes[shade] : CellBrushes[shade];
	}

	// 11 steps of one colour from one opacity to another (no brush per cell per frame)
	private static IBrush[] CreateShades(Color color, double from, double to) =>
		[
			.. Enumerable
				.Range(0, 11)
				.Select(i =>
					(IBrush)new ImmutableSolidColorBrush(color, from + (to - from) * i / 10)
				),
		];
}
