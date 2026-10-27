using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using LT.App.Helpers;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.Indicators;
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
/// <para>
/// Indicators (<see cref="Indicators"/>) draw on the price chart, on the volume bars or in
/// panes of their own stacked under the price chart; every pane shares the horizontal viewport
/// and has its own vertical scale (<see cref="ChartPane"/>). Each pane has a legend at its top
/// left (CandlestickChart.Indicators.cs) whose rows raise <see cref="IndicatorAction"/>.
/// </para>
/// <para>
/// With <see cref="FitBars"/> (a chosen period) a new series zooms out to show all of its bars.
/// </para>
/// <para>
/// With <see cref="RegularHours"/> set (intraday, pre- / post-market shown), bars outside the
/// regular session have a lighter background behind them and hollow candles.
/// </para>
/// <para>
/// Price alerts (<see cref="Alerts"/>) are lines that drag to a new level and have a ✕ to remove
/// them (CandlestickChart.Alerts.cs).
/// </para>
/// </summary>
public partial class CandlestickChart : Control
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

	public static readonly StyledProperty<TimeZoneInfo?> TimeZoneProperty =
		AvaloniaProperty.Register<CandlestickChart, TimeZoneInfo?>(nameof(TimeZone));

	public static readonly StyledProperty<IReadOnlyList<IndicatorResultDto>?> IndicatorsProperty =
		AvaloniaProperty.Register<CandlestickChart, IReadOnlyList<IndicatorResultDto>?>(
			nameof(Indicators)
		);

	public static readonly StyledProperty<TradingScheduleDto?> RegularHoursProperty =
		AvaloniaProperty.Register<CandlestickChart, TradingScheduleDto?>(nameof(RegularHours));

	public static readonly StyledProperty<bool> FitBarsProperty = AvaloniaProperty.Register<
		CandlestickChart,
		bool
	>(nameof(FitBars));

	private const double PriceAxisWidth = 72;
	private const double TimeAxisHeight = 22;
	private const double TopPadding = 24;
	private const double VolumeFraction = 0.18;
	private const double MinBarSpacing = 2;
	private const double MaxBarSpacing = 40;
	private const double DefaultBarSpacing = 8;
	private const double PaneGap = 6;

	private static readonly ImmutableSolidColorBrush Background = new ImmutableSolidColorBrush(
		Color.Parse("#15181D")
	);
	private static readonly ImmutableSolidColorBrush ExtendedHoursBrush =
		new ImmutableSolidColorBrush(Color.Parse("#1C2129"));
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
	private static readonly ImmutableSolidColorBrush AxisLabelBrush = new ImmutableSolidColorBrush(
		Color.Parse("#3A4150")
	);
	private static readonly Cursor ResizeCursor = new(StandardCursorType.SizeWestEast);
	private static readonly TimeZoneInfo TradeDayZone = TradeJournalHelpers.GetTradeDayTimeZone();

	private double _barSpacing = DefaultBarSpacing;
	private int _offset;

	// The next render fits every bar into the plot (FitBars: a new series, double-click)
	private bool _fitPending;
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

	// Indicator panes as last drawn (for the crosshair and hit tests)
	private ChartPane[] _panes = [];

	static CandlestickChart()
	{
		AffectsRender<CandlestickChart>(
			BarsProperty,
			IsIntradayProperty,
			TimeframeProperty,
			FillsProperty,
			TradeLegsProperty,
			BreakEvenPriceProperty,
			RightMarginProperty,
			TimeZoneProperty,
			IndicatorsProperty,
			RegularHoursProperty,
			AlertsProperty,
			IsPlacingAlertProperty
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

	/// <summary>Zone of the time axis and the readout (the display zone); null is the local zone.</summary>
	public TimeZoneInfo? TimeZone
	{
		get => GetValue(TimeZoneProperty);
		set => SetValue(TimeZoneProperty, value);
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

	/// <summary>
	/// Regular sessions of the charted contract; bars outside them are drawn as pre- / post-market.
	/// Null: every bar is regular (daily bars, regular hours only, or no hours from IB).
	/// </summary>
	public TradingScheduleDto? RegularHours
	{
		get => GetValue(RegularHoursProperty);
		set => SetValue(RegularHoursProperty, value);
	}

	/// <summary>Computed indicators, in legend order (hidden ones too, for their legend row).</summary>
	public IReadOnlyList<IndicatorResultDto>? Indicators
	{
		get => GetValue(IndicatorsProperty);
		set => SetValue(IndicatorsProperty, value);
	}

	/// <summary>A legend row was clicked: the indicator instance and what to do with it.</summary>
	public event Action<string, ChartIndicatorAction>? IndicatorAction;

	/// <summary>
	/// Show every bar: a new series (and double-click) zooms out until all of it fits, instead of
	/// the latest bars at the default zoom. Set for a chosen period, so its whole span is in view.
	/// </summary>
	public bool FitBars
	{
		get => GetValue(FitBarsProperty);
		set => SetValue(FitBarsProperty, value);
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
		if (change.Property == IsPlacingAlertProperty)
			UpdateHandleHover(_pointer);
		if (change.Property == FitBarsProperty)
		{
			ResetView();
			return;
		}
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
			ResetView();
	}

	// Back to the latest bar, at the default zoom or (FitBars) with every bar in view
	private void ResetView()
	{
		_offset = 0;
		_barSpacing = DefaultBarSpacing;
		_fitPending = FitBars;
		InvalidateVisual();
	}

	// Which visible bars (start..end) are outside the regular session; all false without hours
	private bool[] GetExtendedBars(IReadOnlyList<BarDto> bars, int start, int end)
	{
		var extended = new bool[Math.Max(0, end - start)];
		if (RegularHours is not { } schedule || !IsIntraday)
			return extended;
		var length = ChartTimeframeHelpers.GetBarLength(Timeframe);
		for (var i = start; i < end; i++)
			extended[i - start] = !TradingHoursHelpers.IsRegular(schedule, bars[i].Time, length);
		return extended;
	}

	public override void Render(DrawingContext context)
	{
		var size = Bounds.Size;
		context.DrawRectangle(Background, null, new Rect(size));
		_legendRows.Clear();
		_legendButtons.Clear();

		var bars = Bars;
		if (bars is not { Count: > 0 } || size.Width < PriceAxisWidth + 20 || size.Height < 80)
		{
			var empty = Text("No data", 13, TextBrush);
			context.DrawText(
				empty,
				new Point((size.Width - empty.Width) / 2, (size.Height - empty.Height) / 2)
			);
			_handleX = null;
			_panes = [];
			_pricePane = null;
			_alertLines.Clear();
			_alertCloseButtons.Clear();
			return;
		}

		var plot = new Rect(
			0,
			TopPadding,
			size.Width - PriceAxisWidth,
			size.Height - TopPadding - TimeAxisHeight
		);
		var indicators = Indicators ?? [];
		var overlays = indicators.Where(x => x.Pane == IndicatorPane.Overlay).ToList();
		var volumeIndicators = indicators.Where(x => x.Pane == IndicatorPane.Volume).ToList();
		var separate = indicators.Where(x => x.Pane == IndicatorPane.Separate).ToList();

		// Price chart (with the volume bars at its bottom) on top, one pane per separate indicator under it
		var paneHeight = IndicatorChartHelpers.GetPaneHeight(plot.Height, separate.Count);
		var mainHeight = plot.Height - paneHeight * separate.Count;
		var volumeHeight = mainHeight * VolumeFraction;
		var priceRect = new Rect(plot.X, plot.Y, plot.Width, mainHeight - volumeHeight - 6);
		var volumeRect = new Rect(
			plot.X,
			plot.Y + mainHeight - volumeHeight,
			plot.Width,
			volumeHeight
		);

		_plot = plot;
		if (_fitPending)
		{
			_fitPending = false;
			_barSpacing = ChartViewportHelpers.GetFitBarSpacing(
				bars.Count,
				RightMargin,
				plot.Width,
				MaxBarSpacing
			);
		}
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
		// Overlays on the price scale widen it, like the bars do
		if (
			IndicatorChartHelpers.GetRange(
				overlays.Where(x => x.IsVisible).SelectMany(x => x.Plots),
				start,
				end
			) is
			{ } overlayRange
		)
		{
			low = Math.Min(low, overlayRange.Min);
			high = Math.Max(high, overlayRange.Max);
		}
		if (high <= low)
		{
			high += 0.5;
			low -= 0.5;
		}
		var padding = (high - low) * 0.06;
		low -= padding;
		high += padding;
		var volumeMax = (double)maxVolume;
		if (
			IndicatorChartHelpers.GetRange(
				volumeIndicators.Where(x => x.IsVisible).SelectMany(x => x.Plots),
				start,
				end
			) is
			{ } volumeRange
		)
			volumeMax = Math.Max(volumeMax, volumeRange.Max);

		var price = new ChartPane(priceRect, low, high);
		var volume = new ChartPane(volumeRect, 0, volumeMax > 0 ? volumeMax : 1);
		var panes = new List<(ChartPane Pane, IndicatorResultDto Result)>();
		for (var k = 0; k < separate.Count; k++)
		{
			var rect = new Rect(
				plot.X,
				plot.Y + mainHeight + k * paneHeight + PaneGap,
				plot.Width,
				Math.Max(1, paneHeight - PaneGap)
			);
			var (min, max) = IndicatorChartHelpers.GetPaneScale(separate[k], start, end);
			panes.Add((new ChartPane(rect, min, max), separate[k]));
		}
		_panes = [.. panes.Select(x => x.Pane)];

		double Y(double value) => price.Y(value);
		double X(int index) =>
			ChartViewportHelpers.GetBarX(index, rightEdge, plot.Right, _barSpacing);
		var view = new Viewport(start, end, _barSpacing, X);

		// pre- / post-market: a lighter band behind each run of bars outside the regular session
		var extended = GetExtendedBars(bars, start, end);
		for (var i = start; i < end; i++)
		{
			if (!extended[i - start])
				continue;
			var first = i;
			while (i + 1 < end && extended[i + 1 - start])
				i++;
			var left = Math.Max(plot.X, X(first) - _barSpacing / 2);
			var right = Math.Min(plot.Right, X(i) + _barSpacing / 2);
			if (right > left)
				context.DrawRectangle(
					ExtendedHoursBrush,
					null,
					new Rect(left, plot.Y, right - left, plot.Height)
				);
		}

		// price grid + axis labels
		var step = NiceStep((high - low) / Math.Max(2, priceRect.Height / 48));
		var decimals = step >= 1 ? 2 : Math.Min(6, (int)Math.Ceiling(-Math.Log10(step)) + 1);
		for (var value = Math.Ceiling(low / step) * step; value <= high; value += step)
		{
			var y = Math.Round(Y(value)) + 0.5;
			context.DrawLine(GridPen, new Point(plot.X, y), new Point(plot.Right, y));
			var label = Text(
				value.ToString("N" + decimals, CultureInfo.CurrentCulture),
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
			var time = ToShown(bars[i].Time);
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
				var top = volume.Y((double)bar.Volume);
				context.DrawRectangle(
					up ? UpVolumeBrush : DownVolumeBrush,
					null,
					new Rect(x - bodyWidth / 2, top, bodyWidth, volumeRect.Bottom - top)
				);
			}

			var wickX = Math.Round(x) + 0.5;
			var pen = up ? UpPen : DownPen;
			var bodyTop = Y(Math.Max(bar.Open, bar.Close));
			var bodyBottom = Y(Math.Min(bar.Open, bar.Close));
			// Pre- / post-market: a hollow body (outline only) the wick doesn't cross
			if (extended[i - start] && bodyWidth >= 3)
			{
				var left = Math.Round(x - bodyWidth / 2) + 0.5;
				var right = Math.Max(left + 2, Math.Round(x + bodyWidth / 2) - 0.5);
				var top = Math.Round(bodyTop) + 0.5;
				var bottom = Math.Max(top + 1, Math.Round(bodyBottom) + 0.5);
				context.DrawLine(pen, new Point(wickX, Y(bar.High)), new Point(wickX, top));
				context.DrawLine(pen, new Point(wickX, bottom), new Point(wickX, Y(bar.Low)));
				context.DrawRectangle(null, pen, new Rect(left, top, right - left, bottom - top));
				continue;
			}
			context.DrawLine(pen, new Point(wickX, Y(bar.High)), new Point(wickX, Y(bar.Low)));
			context.DrawRectangle(
				up ? UpBrush : DownBrush,
				null,
				new Rect(x - bodyWidth / 2, bodyTop, bodyWidth, Math.Max(1, bodyBottom - bodyTop))
			);
		}

		DrawIndicatorPlots(context, volumeIndicators, volume, view, bars, false);
		DrawIndicatorPlots(context, overlays, price, view, bars, true);
		foreach (var (pane, result) in panes)
			DrawIndicatorPane(context, pane, result, view, bars);

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

		DrawAlerts(context, price, decimals);

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

		// crosshair: through every pane, the value of the pane under the pointer on the axis
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
				DrawAxisLabel(context, plot.Right, p.Y, price.Value(p.Y), decimals);
			else
				foreach (var (pane, result) in panes)
					if (pane.Contains(p.Y))
					{
						DrawAxisLabel(
							context,
							plot.Right,
							p.Y,
							pane.Value(p.Y),
							GetDecimals(result, pane)
						);
						break;
					}
		}

		DrawAlertPreview(context, bars[^1].Close);

		var b = bars[hovered];
		var readout = Text(
			$"{FormatTime(ToShown(b.Time), null)}   O {b.Open.ToString("N" + decimals)}   H {b.High.ToString("N" + decimals)}   L {b.Low.ToString("N" + decimals)}   C {b.Close.ToString("N" + decimals)}   V {b.Volume:#,0}",
			12,
			b.Close >= b.Open ? UpBrush : DownBrush
		);
		context.DrawText(readout, new Point(6, 4));

		// the hovered bar's fills under the readout
		var legendTop = priceRect.Top + 4;
		var hoveredFills = _fillBars.Where(x => x.Index == hovered).Select(x => x.Fill).ToList();
		if (hoveredFills.Count > 0)
		{
			const int shown = 4;
			var text = string.Join(
				"   ",
				hoveredFills
					.Take(shown)
					.Select(x =>
						$"{(TradeMatchingHelpers.GetDirection(x.Side) > 0 ? "Buy" : "Sell")} {Sensitive.Apply(Math.Abs(x.Shares).ToString("#,0.####", CultureInfo.CurrentCulture))} @ {x.Price.ToString("N" + decimals, CultureInfo.CurrentCulture)}"
					)
			);
			if (hoveredFills.Count > shown)
				text += $"   +{hoveredFills.Count - shown} more";
			var fills = Text(text, 11, LabelBrush);
			context.DrawText(fills, new Point(6, 4 + readout.Height));
			legendTop = Math.Max(legendTop, 4 + readout.Height + fills.Height + 2);
		}

		// legends: overlays under the readout, volume indicators on the volume bars, each pane its own
		DrawLegend(context, overlays, new Point(6, legendTop), plot.Width, hovered, _ => decimals);
		DrawLegend(
			context,
			volumeIndicators,
			new Point(6, volumeRect.Top + 2),
			plot.Width,
			hovered,
			x => x.Decimals ?? 0
		);
		foreach (var (pane, result) in panes)
			DrawLegend(
				context,
				[result],
				new Point(6, pane.Rect.Top + 2),
				plot.Width,
				hovered,
				x => GetDecimals(x, pane)
			);
	}

	private void DrawAxisLabel(
		DrawingContext context,
		double x,
		double y,
		double value,
		int decimals
	)
	{
		var label = Text(
			value.ToString("N" + decimals, CultureInfo.CurrentCulture),
			11,
			LabelBrush
		);
		context.DrawRectangle(
			AxisLabelBrush,
			null,
			new Rect(x, y - label.Height / 2 - 2, PriceAxisWidth, label.Height + 4)
		);
		context.DrawText(label, new Point(x + 6, y - label.Height / 2));
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
		_handleHovered =
			position is { } p && _dragStart == null && !IsPlacingAlert && IsOnMarginHandle(p);
		// Placing an alert: a click places it, so nothing under the pointer reads as clickable
		var dragging =
			_dragStart != null
			|| _marginDragStartX != null
			|| _draggedAlert != null
			|| IsPlacingAlert;
		UpdateLegendHover(dragging ? null : position);
		Cursor =
			GetAlertCursor(position) is { } alertCursor ? alertCursor
			: _handleHovered || _marginDragStartX != null ? ResizeCursor
			: !dragging && position is { } q && HitLegend(q) != null ? HandCursor
			: null;
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
		if (_draggedAlert != null)
			DragAlert(position);
		else if (_marginDragStartX is { } marginStart)
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
		if (OnAlertPlacementPressed(e, position))
		{
			UpdateHandleHover(position);
			return;
		}
		if (HitLegend(position) is { } legend)
		{
			e.Handled = true;
			if (e.ClickCount == 1 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
				RaiseIndicatorAction(legend.InstanceId, legend.Action);
			return;
		}
		// Alt+A on the chart adds an alert, so a click gives it the keyboard
		Focus();
		// The right button is the chart's context menu (opened on release): no pan, no reset
		if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
		{
			MenuPoint = position;
			return;
		}
		if (OnAlertPressed(e, position))
		{
			e.Handled = true;
			UpdateHandleHover(position);
			return;
		}
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
			ResetView();
			return;
		}
		_dragStart = position;
		_dragStartOffset = _offset;
		e.Pointer.Capture(this);
	}

	protected override void OnPointerReleased(PointerReleasedEventArgs e)
	{
		base.OnPointerReleased(e);
		OnAlertReleased();
		_dragStart = null;
		_marginDragStartX = null;
		e.Pointer.Capture(null);
		UpdateHandleHover(e.GetPosition(this));
		InvalidateVisual();
	}

	protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
	{
		base.OnPointerCaptureLost(e);
		// Lost before the release (another window took the pointer): the alert stays where it was
		_draggedAlert = null;
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
		// A fitted long period may be denser than the wheel goes; zooming out never jumps in
		_barSpacing = Math.Clamp(
			e.Delta.Y > 0 ? _barSpacing * 1.15 : _barSpacing / 1.15,
			Math.Min(MinBarSpacing, _barSpacing),
			MaxBarSpacing
		);
		e.Handled = true;
		InvalidateVisual();
	}

	// Intraday bar times in the display zone; daily and weekly bars carry a calendar date
	// (midnight UTC), shown as it is so the date doesn't shift with the zone
	private DateTimeOffset ToShown(DateTimeOffset time) =>
		IsIntraday ? DisplayTimeHelpers.ToZone(time, TimeZone ?? TimeZoneInfo.Local) : time;

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
