using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.Performance;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

/// <summary>
/// Lightweight OHLC + volume chart. Wheel zooms, drag pans, double-click resets, hover shows the
/// bar under the cursor. Optionally the user's fills (buy ▲ / sell ▼ at their price, on the bar
/// that contains their time), lines from each entry to the exit that closed it (green: won, red:
/// lost) and the open position's break-even line. A handle in the time axis, at the end of the
/// last bar, drags out empty space after it (<see cref="RightMargin"/>, in bars, so it keeps its
/// place across zoom, new bars and timeframe switches); double-clicking the handle removes it.
/// </summary>
public class CandlestickChart : Control
{
	public static readonly StyledProperty<IReadOnlyList<BarDto>?> BarsProperty =
		AvaloniaProperty.Register<CandlestickChart, IReadOnlyList<BarDto>?>(nameof(Bars));

	public static readonly StyledProperty<bool> IsIntradayProperty = AvaloniaProperty.Register<
		CandlestickChart,
		bool
	>(nameof(IsIntraday));

	public static readonly StyledProperty<ChartTimeframe> TimeframeProperty =
		AvaloniaProperty.Register<CandlestickChart, ChartTimeframe>(nameof(Timeframe));

	public static readonly StyledProperty<IReadOnlyList<ExecutionDto>?> FillsProperty =
		AvaloniaProperty.Register<CandlestickChart, IReadOnlyList<ExecutionDto>?>(nameof(Fills));

	public static readonly StyledProperty<IReadOnlyList<TradeLegDto>?> TradeLegsProperty =
		AvaloniaProperty.Register<CandlestickChart, IReadOnlyList<TradeLegDto>?>(nameof(TradeLegs));

	public static readonly StyledProperty<double?> BreakEvenPriceProperty =
		AvaloniaProperty.Register<CandlestickChart, double?>(nameof(BreakEvenPrice));

	public static readonly StyledProperty<double> RightMarginProperty = AvaloniaProperty.Register<
		CandlestickChart,
		double
	>(nameof(RightMargin), defaultBindingMode: BindingMode.TwoWay);

	private const double PriceAxisWidth = 72;
	private const double TimeAxisHeight = 22;
	private const double TopPadding = 24;
	private const double VolumeFraction = 0.18;
	private const double MinBarSpacing = 2;
	private const double MaxBarSpacing = 40;
	private const double DefaultBarSpacing = 8;

	private static readonly ImmutableSolidColorBrush Background = new ImmutableSolidColorBrush(
		Color.Parse("#15181D")
	);
	private static readonly ImmutableSolidColorBrush UpBrush = new ImmutableSolidColorBrush(
		Color.Parse("#26A69A")
	);
	private static readonly ImmutableSolidColorBrush DownBrush = new ImmutableSolidColorBrush(
		Color.Parse("#EF5350")
	);
	private static readonly ImmutableSolidColorBrush UpVolumeBrush = new ImmutableSolidColorBrush(
		Color.Parse("#5526A69A")
	);
	private static readonly ImmutableSolidColorBrush DownVolumeBrush = new ImmutableSolidColorBrush(
		Color.Parse("#55EF5350")
	);
	private static readonly ImmutableSolidColorBrush TextBrush = new ImmutableSolidColorBrush(
		Color.Parse("#8A93A3")
	);
	private static readonly ImmutableSolidColorBrush LabelBrush = new ImmutableSolidColorBrush(
		Color.Parse("#E6E8EB")
	);
	private static readonly ImmutableSolidColorBrush LastPriceBrush = new ImmutableSolidColorBrush(
		Color.Parse("#4C8DFF")
	);
	private static readonly IPen GridPen = new ImmutablePen(
		new ImmutableSolidColorBrush(Color.Parse("#222731")),
		1
	);
	private static readonly IPen CrosshairPen = new ImmutablePen(
		new ImmutableSolidColorBrush(Color.Parse("#6B7385")),
		1,
		new ImmutableDashStyle([3, 3], 0)
	);
	private static readonly IPen LastPricePen = new ImmutablePen(
		LastPriceBrush,
		1,
		new ImmutableDashStyle([2, 3], 0)
	);
	private static readonly IPen UpPen = new ImmutablePen(UpBrush, 1);
	private static readonly IPen DownPen = new ImmutablePen(DownBrush, 1);
	private static readonly ImmutableSolidColorBrush BreakEvenBrush = new ImmutableSolidColorBrush(
		Color.Parse("#FFB300")
	);
	private static readonly IPen BreakEvenPen = new ImmutablePen(
		BreakEvenBrush,
		1,
		new ImmutableDashStyle([6, 3], 0)
	);
	private static readonly IPen MarkerOutlinePen = new ImmutablePen(LabelBrush, 1);
	private static readonly IPen WinLegPen = new ImmutablePen(
		UpBrush,
		1.5,
		new ImmutableDashStyle([4, 3], 0)
	);
	private static readonly IPen LossLegPen = new ImmutablePen(
		DownBrush,
		1.5,
		new ImmutableDashStyle([4, 3], 0)
	);
	private static readonly IPen MarginGuidePen = new ImmutablePen(
		TextBrush,
		1,
		new ImmutableDashStyle([2, 3], 0)
	);
	private static readonly Cursor ResizeCursor = new(StandardCursorType.SizeWestEast);
	private static readonly TimeZoneInfo TradeDayZone = TradeJournalHelpers.GetTradeDayTimeZone();

	private double _barSpacing = DefaultBarSpacing;
	private int _offset;
	private Point? _pointer;
	private Point? _dragStart;
	private int _dragStartOffset;

	// Margin handle: where it was last drawn (null: off the plot) and its drag
	private Rect _plot;
	private double? _handleX;
	private bool _handleHovered;
	private double? _marginDragStartX;
	private double _marginDragStartValue;

	// Bar index of each fill / leg end; rebuilt when the bars, fills or timeframe change
	private (int Index, ExecutionDto Fill)[] _fillBars = [];
	private (int Entry, int Exit, TradeLegDto Leg)[] _legBars = [];
	private bool _tradesDirty = true;

	static CandlestickChart()
	{
		AffectsRender<CandlestickChart>(
			BarsProperty,
			IsIntradayProperty,
			TimeframeProperty,
			FillsProperty,
			TradeLegsProperty,
			BreakEvenPriceProperty,
			RightMarginProperty
		);
		ClipToBoundsProperty.OverrideDefaultValue<CandlestickChart>(true);
		FocusableProperty.OverrideDefaultValue<CandlestickChart>(true);
	}

	public IReadOnlyList<BarDto>? Bars
	{
		get => GetValue(BarsProperty);
		set => SetValue(BarsProperty, value);
	}

	public bool IsIntraday
	{
		get => GetValue(IsIntradayProperty);
		set => SetValue(IsIntradayProperty, value);
	}

	/// <summary>Bar size, to tell which bar a fill's time falls in.</summary>
	public ChartTimeframe Timeframe
	{
		get => GetValue(TimeframeProperty);
		set => SetValue(TimeframeProperty, value);
	}

	public IReadOnlyList<ExecutionDto>? Fills
	{
		get => GetValue(FillsProperty);
		set => SetValue(FillsProperty, value);
	}

	public IReadOnlyList<TradeLegDto>? TradeLegs
	{
		get => GetValue(TradeLegsProperty);
		set => SetValue(TradeLegsProperty, value);
	}

	public double? BreakEvenPrice
	{
		get => GetValue(BreakEvenPriceProperty);
		set => SetValue(BreakEvenPriceProperty, value);
	}

	/// <summary>Empty bars after the latest one (fractions allowed).</summary>
	public double RightMargin
	{
		get => GetValue(RightMarginProperty);
		set => SetValue(RightMarginProperty, value);
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (
			change.Property == BarsProperty
			|| change.Property == FillsProperty
			|| change.Property == TradeLegsProperty
			|| change.Property == TimeframeProperty
		)
			_tradesDirty = true;
		if (change.Property != BarsProperty)
			return;

		// A different series (new contract / timeframe) starts at the latest bar again
		var oldBars = change.GetOldValue<IReadOnlyList<BarDto>?>();
		var newBars = change.GetNewValue<IReadOnlyList<BarDto>?>();
		if (
			oldBars is not { Count: > 0 }
			|| newBars is not { Count: > 0 }
			|| oldBars[0].Time != newBars[0].Time
		)
		{
			_offset = 0;
			_barSpacing = DefaultBarSpacing;
		}
	}

	public override void Render(DrawingContext context)
	{
		var size = Bounds.Size;
		context.DrawRectangle(Background, null, new Rect(size));

		var bars = Bars;
		if (bars is not { Count: > 0 } || size.Width < PriceAxisWidth + 20 || size.Height < 80)
		{
			var empty = Text("No data", 13, TextBrush);
			context.DrawText(
				empty,
				new Point((size.Width - empty.Width) / 2, (size.Height - empty.Height) / 2)
			);
			_handleX = null;
			return;
		}

		var plot = new Rect(
			0,
			TopPadding,
			size.Width - PriceAxisWidth,
			size.Height - TopPadding - TimeAxisHeight
		);
		var volumeHeight = plot.Height * VolumeFraction;
		var priceRect = new Rect(plot.X, plot.Y, plot.Width, plot.Height - volumeHeight - 6);
		var volumeRect = new Rect(plot.X, plot.Bottom - volumeHeight, plot.Width, volumeHeight);

		_plot = plot;
		_offset = Math.Clamp(_offset, 0, bars.Count - 1);
		var rightMargin = ChartViewportHelpers.ClampRightMargin(
			RightMargin,
			plot.Width,
			_barSpacing
		);
		var rightEdge = ChartViewportHelpers.GetRightEdge(bars.Count, _offset, rightMargin);
		var (start, end) = ChartViewportHelpers.GetVisibleRange(
			bars.Count,
			rightEdge,
			plot.Width,
			_barSpacing
		);

		var low = double.MaxValue;
		var high = double.MinValue;
		decimal maxVolume = 0;
		for (var i = start; i < end; i++)
		{
			low = Math.Min(low, bars[i].Low);
			high = Math.Max(high, bars[i].High);
			maxVolume = Math.Max(maxVolume, bars[i].Volume);
		}
		if (high <= low)
		{
			high += 0.5;
			low -= 0.5;
		}
		var padding = (high - low) * 0.06;
		low -= padding;
		high += padding;

		double Y(double price) =>
			priceRect.Bottom - (price - low) / (high - low) * priceRect.Height;
		double X(int index) =>
			ChartViewportHelpers.GetBarX(index, rightEdge, plot.Right, _barSpacing);

		// price grid + axis labels
		var step = NiceStep((high - low) / Math.Max(2, priceRect.Height / 48));
		var decimals = step >= 1 ? 2 : Math.Min(6, (int)Math.Ceiling(-Math.Log10(step)) + 1);
		for (var price = Math.Ceiling(low / step) * step; price <= high; price += step)
		{
			var y = Math.Round(Y(price)) + 0.5;
			context.DrawLine(GridPen, new Point(plot.X, y), new Point(plot.Right, y));
			var label = Text(
				price.ToString("N" + decimals, CultureInfo.CurrentCulture),
				11,
				TextBrush
			);
			context.DrawText(label, new Point(plot.Right + 6, y - label.Height / 2));
		}

		// time labels
		var labelEvery = Math.Max(1, (int)Math.Ceiling(96 / _barSpacing));
		DateTimeOffset? previous = null;
		for (var i = start; i < end; i++)
		{
			if ((bars.Count - 1 - i) % labelEvery != 0)
				continue;
			var time = bars[i].Time.ToLocalTime();
			var x = Math.Round(X(i)) + 0.5;
			context.DrawLine(GridPen, new Point(x, plot.Y), new Point(x, plot.Bottom));
			var label = Text(FormatTime(time, previous), 11, TextBrush);
			context.DrawText(label, new Point(x - label.Width / 2, plot.Bottom + 4));
			previous = time;
		}

		// volume + candles
		var bodyWidth = Math.Max(1, _barSpacing * 0.7);
		for (var i = start; i < end; i++)
		{
			var bar = bars[i];
			var up = bar.Close >= bar.Open;
			var x = X(i);

			if (maxVolume > 0 && bar.Volume > 0)
			{
				var h = (double)(bar.Volume / maxVolume) * volumeRect.Height;
				context.DrawRectangle(
					up ? UpVolumeBrush : DownVolumeBrush,
					null,
					new Rect(x - bodyWidth / 2, volumeRect.Bottom - h, bodyWidth, h)
				);
			}

			var wickX = Math.Round(x) + 0.5;
			context.DrawLine(
				up ? UpPen : DownPen,
				new Point(wickX, Y(bar.High)),
				new Point(wickX, Y(bar.Low))
			);
			var top = Y(Math.Max(bar.Open, bar.Close));
			var bottom = Y(Math.Min(bar.Open, bar.Close));
			context.DrawRectangle(
				up ? UpBrush : DownBrush,
				null,
				new Rect(x - bodyWidth / 2, top, bodyWidth, Math.Max(1, bottom - top))
			);
		}

		DrawTrades(context, bars, priceRect, X, Y);
		DrawMarginHandle(context, plot, bars.Count, rightEdge);

		// break-even of the open position
		if (BreakEvenPrice is { } breakEven)
		{
			var beY = Math.Round(Y(breakEven)) + 0.5;
			if (beY >= priceRect.Top && beY <= priceRect.Bottom)
			{
				context.DrawLine(BreakEvenPen, new Point(plot.X, beY), new Point(plot.Right, beY));
				var tag = Text("Break-even", 11, BreakEvenBrush);
				context.DrawText(tag, new Point(plot.X + 6, beY - tag.Height - 1));
				var label = Text(
					breakEven.ToString("N" + decimals, CultureInfo.CurrentCulture),
					11,
					Background
				);
				context.DrawRectangle(
					BreakEvenBrush,
					null,
					new Rect(
						plot.Right,
						beY - label.Height / 2 - 2,
						PriceAxisWidth,
						label.Height + 4
					)
				);
				context.DrawText(label, new Point(plot.Right + 6, beY - label.Height / 2));
			}
		}

		// last price
		var last = bars[^1].Close;
		var lastY = Math.Round(Y(last)) + 0.5;
		if (lastY >= priceRect.Top && lastY <= priceRect.Bottom)
		{
			context.DrawLine(LastPricePen, new Point(plot.X, lastY), new Point(plot.Right, lastY));
			var label = Text(
				last.ToString("N" + decimals, CultureInfo.CurrentCulture),
				11,
				LabelBrush
			);
			context.DrawRectangle(
				LastPriceBrush,
				null,
				new Rect(plot.Right, lastY - label.Height / 2 - 2, PriceAxisWidth, label.Height + 4)
			);
			context.DrawText(label, new Point(plot.Right + 6, lastY - label.Height / 2));
		}

		// crosshair + readout
		var hovered = end - 1;
		if (_pointer is { } p && plot.Contains(p))
		{
			context.DrawLine(CrosshairPen, new Point(p.X, plot.Y), new Point(p.X, plot.Bottom));
			context.DrawLine(CrosshairPen, new Point(plot.X, p.Y), new Point(plot.Right, p.Y));
			hovered = Math.Clamp(
				ChartViewportHelpers.GetBarIndexAt(p.X, rightEdge, plot.Right, _barSpacing),
				start,
				end - 1
			);
			if (p.Y <= priceRect.Bottom)
			{
				var price = low + (priceRect.Bottom - p.Y) / priceRect.Height * (high - low);
				var label = Text(
					price.ToString("N" + decimals, CultureInfo.CurrentCulture),
					11,
					LabelBrush
				);
				context.DrawRectangle(
					new ImmutableSolidColorBrush(Color.Parse("#3A4150")),
					null,
					new Rect(
						plot.Right,
						p.Y - label.Height / 2 - 2,
						PriceAxisWidth,
						label.Height + 4
					)
				);
				context.DrawText(label, new Point(plot.Right + 6, p.Y - label.Height / 2));
			}
		}

		var b = bars[hovered];
		var readout = Text(
			$"{FormatTime(b.Time.ToLocalTime(), null)}   O {b.Open.ToString("N" + decimals)}   H {b.High.ToString("N" + decimals)}   L {b.Low.ToString("N" + decimals)}   C {b.Close.ToString("N" + decimals)}   V {b.Volume:#,0}",
			12,
			b.Close >= b.Open ? UpBrush : DownBrush
		);
		context.DrawText(readout, new Point(6, 4));

		// the hovered bar's fills under the readout
		var hoveredFills = _fillBars.Where(x => x.Index == hovered).Select(x => x.Fill).ToList();
		if (hoveredFills.Count > 0)
		{
			const int shown = 4;
			var text = string.Join(
				"   ",
				hoveredFills
					.Take(shown)
					.Select(x =>
						$"{(TradeMatchingHelpers.GetDirection(x.Side) > 0 ? "Buy" : "Sell")} {Math.Abs(x.Shares):#,0.####} @ {x.Price.ToString("N" + decimals, CultureInfo.CurrentCulture)}"
					)
			);
			if (hoveredFills.Count > shown)
				text += $"   +{hoveredFills.Count - shown} more";
			context.DrawText(Text(text, 11, LabelBrush), new Point(6, 4 + readout.Height));
		}
	}

	// The break line between the last bar and the right margin: a grip in the time axis, and a
	// guide through the plot while it is hovered or dragged. Hidden when panned past it.
	private void DrawMarginHandle(DrawingContext context, Rect plot, int barCount, double rightEdge)
	{
		var x = Math.Round(
			ChartViewportHelpers.GetMarginHandleX(barCount, rightEdge, plot.Right, _barSpacing)
		);
		if (x < plot.X || x > plot.Right)
		{
			_handleX = null;
			return;
		}
		_handleX = x;
		var active = _handleHovered || _marginDragStartX != null;
		if (active)
			context.DrawLine(
				MarginGuidePen,
				new Point(x + 0.5, plot.Y),
				new Point(x + 0.5, plot.Bottom)
			);
		context.DrawRectangle(
			active ? LabelBrush : TextBrush,
			null,
			new Rect(x - 1, plot.Bottom + 3, 3, TimeAxisHeight - 6),
			1.5,
			1.5
		);
	}

	private bool IsOnMarginHandle(Point position) =>
		_handleX is { } x
		&& position.Y >= _plot.Bottom
		&& position.Y <= Bounds.Height
		&& Math.Abs(position.X - x) <= ChartConstants.MarginHandleHitWidth;

	private void UpdateHandleHover(Point? position)
	{
		_handleHovered = position is { } p && _dragStart == null && IsOnMarginHandle(p);
		Cursor = _handleHovered || _marginDragStartX != null ? ResizeCursor : null;
	}

	private void DrawTrades(
		DrawingContext context,
		IReadOnlyList<BarDto> bars,
		Rect priceRect,
		Func<int, double> x,
		Func<double, double> y
	)
	{
		if (_tradesDirty)
		{
			_tradesDirty = false;
			var timeframe = Timeframe;
			_fillBars = (Fills ?? [])
				.Where(f => f.Time != null && TradeMatchingHelpers.GetDirection(f.Side) != 0)
				.Select(f =>
					(
						ChartTradeHelpers.FindBarIndex(
							bars,
							f.Time!.Value,
							timeframe,
							TradeDayZone
						),
						f
					)
				)
				.Where(f => f.Item1 >= 0)
				.ToArray();
			_legBars = (TradeLegs ?? [])
				.Select(l =>
					(
						ChartTradeHelpers.FindBarIndex(bars, l.EntryTime, timeframe, TradeDayZone),
						ChartTradeHelpers.FindBarIndex(bars, l.ExitTime, timeframe, TradeDayZone),
						l
					)
				)
				.Where(l => l.Item1 >= 0 && l.Item2 >= 0)
				.ToArray();
		}
		if (_fillBars.Length == 0 && _legBars.Length == 0)
			return;

		using var clip = context.PushClip(priceRect);
		foreach (var (entry, exit, leg) in _legBars)
			context.DrawLine(
				leg.NetPnl >= 0 ? WinLegPen : LossLegPen,
				new Point(x(entry), y(leg.EntryPrice)),
				new Point(x(exit), y(leg.ExitPrice))
			);

		// Tip at the fill price: a buy points up from below it, a sell down from above it
		var width = Math.Clamp(_barSpacing * 0.9, 7, 12);
		var height = width * 0.85;
		foreach (var (index, fill) in _fillBars)
		{
			var px = x(index);
			if (px < priceRect.Left - width || px > priceRect.Right + width)
				continue;
			var py = y(fill.Price);
			var buy = TradeMatchingHelpers.GetDirection(fill.Side) > 0;
			var baseY = buy ? py + height : py - height;
			var geometry = new StreamGeometry();
			using (var g = geometry.Open())
			{
				g.BeginFigure(new Point(px, py), true);
				g.LineTo(new Point(px - width / 2, baseY));
				g.LineTo(new Point(px + width / 2, baseY));
				g.EndFigure(true);
			}
			context.DrawGeometry(buy ? UpBrush : DownBrush, MarkerOutlinePen, geometry);
		}
	}

	protected override void OnPointerMoved(PointerEventArgs e)
	{
		base.OnPointerMoved(e);
		var position = e.GetPosition(this);
		_pointer = position;
		if (_marginDragStartX is { } marginStart)
			RightMargin = ChartViewportHelpers.DragRightMargin(
				_marginDragStartValue,
				position.X - marginStart,
				_plot.Width,
				_barSpacing
			);
		else if (_dragStart is { } start)
			_offset = Math.Max(
				0,
				_dragStartOffset + (int)Math.Round((position.X - start.X) / _barSpacing)
			);
		UpdateHandleHover(position);
		InvalidateVisual();
	}

	protected override void OnPointerPressed(PointerPressedEventArgs e)
	{
		base.OnPointerPressed(e);
		var position = e.GetPosition(this);
		if (IsOnMarginHandle(position))
		{
			if (e.ClickCount == 2)
				RightMargin = 0;
			else
			{
				_marginDragStartX = position.X;
				_marginDragStartValue = ChartViewportHelpers.ClampRightMargin(
					RightMargin,
					_plot.Width,
					_barSpacing
				);
				e.Pointer.Capture(this);
			}
			UpdateHandleHover(position);
			InvalidateVisual();
			return;
		}
		if (e.ClickCount == 2)
		{
			_offset = 0;
			_barSpacing = DefaultBarSpacing;
			InvalidateVisual();
			return;
		}
		_dragStart = position;
		_dragStartOffset = _offset;
		e.Pointer.Capture(this);
	}

	protected override void OnPointerReleased(PointerReleasedEventArgs e)
	{
		base.OnPointerReleased(e);
		_dragStart = null;
		_marginDragStartX = null;
		e.Pointer.Capture(null);
		UpdateHandleHover(e.GetPosition(this));
		InvalidateVisual();
	}

	protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
	{
		base.OnPointerCaptureLost(e);
		_dragStart = null;
		_marginDragStartX = null;
		UpdateHandleHover(_pointer);
	}

	protected override void OnPointerExited(PointerEventArgs e)
	{
		base.OnPointerExited(e);
		_pointer = null;
		UpdateHandleHover(null);
		InvalidateVisual();
	}

	protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
	{
		base.OnPointerWheelChanged(e);
		_barSpacing = Math.Clamp(
			e.Delta.Y > 0 ? _barSpacing * 1.15 : _barSpacing / 1.15,
			MinBarSpacing,
			MaxBarSpacing
		);
		e.Handled = true;
		InvalidateVisual();
	}

	private string FormatTime(DateTimeOffset time, DateTimeOffset? previous)
	{
		if (!IsIntraday)
			return previous is { } p && p.Year != time.Year
				? time.ToString("yyyy")
				: time.ToString("dd MMM yy");
		return previous is { } q && q.Date == time.Date
			? time.ToString("HH:mm")
			: time.ToString("dd MMM HH:mm");
	}

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
