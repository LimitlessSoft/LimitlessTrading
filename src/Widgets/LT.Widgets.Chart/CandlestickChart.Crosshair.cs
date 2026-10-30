using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

/// <summary>
/// Crosshair mode (<see cref="IsCrosshairMode"/>, F on the chart): the crosshair's vertical line
/// snaps to the bar under the pointer, its time is highlighted on the time axis and the price (or
/// the value of the indicator pane under the pointer) on the price axis. Pressing and dragging on
/// the price chart draws a ruler from the press to the pointer: a box, green going up and red
/// going down, labelled with the price change, its percentage, the bars and the time. It stays
/// after the release and goes on the next press anywhere (a press that isn't dragged only clears
/// it) or <see cref="ClearRuler"/>. While the mode is on, a press on the price chart measures:
/// it doesn't pan, drag an order or alert line or pick a drawing; an armed tool (alert, drawing)
/// still takes the click.
/// </summary>
public partial class CandlestickChart
{
	public static readonly StyledProperty<bool> IsCrosshairModeProperty = AvaloniaProperty.Register<
		CandlestickChart,
		bool
	>(nameof(IsCrosshairMode));

	private static readonly IPen CrosshairModePen = new ImmutablePen(
		new ImmutableSolidColorBrush(Color.Parse("#9AA3B5")),
		1,
		new ImmutableDashStyle([4, 3], 0)
	);
	private static readonly ImmutableSolidColorBrush CrosshairLabelBrush = new(
		Color.Parse("#5D6679")
	);
	private static readonly ImmutableSolidColorBrush RulerUpFill = new(Color.Parse("#3326A69A"));
	private static readonly ImmutableSolidColorBrush RulerDownFill = new(Color.Parse("#33EF5350"));

	// Own brushes: static fields of another part of the class may not be set yet when these are
	private static readonly ImmutableSolidColorBrush RulerUpBrush = new(Color.Parse("#26A69A"));
	private static readonly ImmutableSolidColorBrush RulerDownBrush = new(Color.Parse("#EF5350"));
	private static readonly IPen RulerUpPen = new ImmutablePen(RulerUpBrush, 1.5);
	private static readonly IPen RulerDownPen = new ImmutablePen(RulerDownBrush, 1.5);
	private static readonly Cursor CrossCursor = new(StandardCursorType.Cross);

	// The ruler's ends (a time on the bars and a price); the end is null until the press is dragged
	private ChartDrawingPointDto? _rulerFrom;
	private ChartDrawingPointDto? _rulerTo;

	// Where the ruler's press was, while the button is down
	private Point? _rulerPressAt;

	/// <summary>The crosshair's lines, labels and ruler are on (not saved; F toggles it).</summary>
	public bool IsCrosshairMode
	{
		get => GetValue(IsCrosshairModeProperty);
		set => SetValue(IsCrosshairModeProperty, value);
	}

	/// <summary>A ruler is drawn (or being dragged).</summary>
	public bool HasRuler => _rulerTo != null;

	// A press on the price chart draws a ruler: crosshair mode with no tool armed
	private bool IsRulerArmed =>
		IsCrosshairMode && !IsPlacingAlert && !IsDrawingToolArmed && CanMapDrawings;

	/// <summary>Removes the ruler (and one being dragged). False when there was none.</summary>
	public bool ClearRuler()
	{
		var had = _rulerFrom != null || _rulerPressAt != null;
		_rulerFrom = null;
		_rulerTo = null;
		_rulerPressAt = null;
		if (had)
			InvalidateVisual();
		return had;
	}

	private void OnCrosshairPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		if (change.Property != IsCrosshairModeProperty)
			return;
		if (!IsCrosshairMode)
			ClearRuler();
		UpdateHandleHover(_pointer);
	}

	// The chart point of a screen point for the ruler: on the bar slot's centre, the price under
	// it kept on the price chart
	private ChartDrawingPointDto ToRulerPoint(Point position)
	{
		var pane = _pricePane!.Value;
		var barPosition = Math.Floor(GetPositionAt(position.X) + 0.5);
		return new ChartDrawingPointDto
		{
			Time = ChartDrawingHelpers.GetTimeAt(Bars!, barPosition, BarLength),
			Price = pane.Value(Math.Clamp(position.Y, pane.Rect.Top, pane.Rect.Bottom)),
		};
	}

	// ---- pointer -------------------------------------------------------------------------

	// Left button on the price chart in crosshair mode: the ruler starts here (shown once
	// dragged); a double-click resets the view. True when handled (nothing else starts)
	private bool OnRulerPressed(PointerPressedEventArgs e, Point position)
	{
		if (
			!IsRulerArmed
			|| !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
			|| !IsOnPriceChart(position)
		)
			return false;
		e.Handled = true;
		ClearRuler();
		if (e.ClickCount == 2)
		{
			ResetView();
			return true;
		}
		_rulerPressAt = position;
		_rulerFrom = ToRulerPoint(position);
		e.Pointer.Capture(this);
		return true;
	}

	// While the button is down: the end follows the pointer once it has moved off the press
	private void DragRuler(Point position)
	{
		if (_rulerPressAt is not { } pressedAt || !CanMapDrawings)
			return;
		if (
			_rulerTo == null
			&& Math.Abs(position.X - pressedAt.X) < ChartDrawingConstants.DragThreshold
			&& Math.Abs(position.Y - pressedAt.Y) < ChartDrawingConstants.DragThreshold
		)
			return;
		_rulerTo = ToRulerPoint(position);
	}

	// Released: a dragged ruler stays, a press that wasn't dragged leaves none
	private void OnRulerReleased()
	{
		if (_rulerPressAt == null)
			return;
		_rulerPressAt = null;
		if (_rulerTo == null)
			_rulerFrom = null;
	}

	// ---- rendering -----------------------------------------------------------------------

	// The ruler: its box on the price chart, a line from the start to the end, the label above
	// the box going up and under it going down (kept on the plot)
	private void DrawRuler(DrawingContext context, Rect priceRect, int decimals)
	{
		if (_rulerFrom is not { } from || _rulerTo is not { } to || !CanMapDrawings)
			return;
		var bars = Bars!;
		var measure = ChartCrosshairHelpers.Measure(
			bars,
			BarLength,
			from.Time,
			from.Price,
			to.Time,
			to.Price
		);
		var a = ToScreen(from);
		var b = ToScreen(to);
		var up = measure.IsUp;
		using (context.PushClip(priceRect))
		{
			context.DrawRectangle(up ? RulerUpFill : RulerDownFill, null, Box(a, b));
			var pen = up ? RulerUpPen : RulerDownPen;
			context.DrawLine(pen, new Point(a.X, a.Y), new Point(b.X, b.Y));
			if (Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y) > 8)
			{
				var (left, right) = ChartDrawingHelpers.GetArrowHead(a, b, 8);
				context.DrawLine(pen, new Point(b.X, b.Y), new Point(left.X, left.Y));
				context.DrawLine(pen, new Point(b.X, b.Y), new Point(right.X, right.Y));
			}
		}

		var label = Text(
			ChartCrosshairHelpers.GetLabel(measure, decimals, CultureInfo.CurrentCulture),
			11,
			LabelBrush
		);
		var width = label.Width + 10;
		var height = label.Height + 6;
		var top = up ? Math.Min(a.Y, b.Y) - height - 4 : Math.Max(a.Y, b.Y) + 4;
		var tag = new Rect(
			Math.Clamp((a.X + b.X) / 2 - width / 2, priceRect.Left, priceRect.Right - width),
			Math.Clamp(top, priceRect.Top, Math.Max(priceRect.Top, priceRect.Bottom - height)),
			width,
			height
		);
		context.DrawRectangle(up ? RulerUpBrush : RulerDownBrush, null, tag, 3, 3);
		context.DrawText(label, new Point(tag.X + 5, tag.Y + 3));
	}

	// The crosshair in crosshair mode: the vertical line on the bar's centre through every pane,
	// the horizontal one at the pointer, the time on the time axis and the value of the pane
	// under the pointer on the price axis, both highlighted
	private void DrawCrosshairMode(
		DrawingContext context,
		Point p,
		Rect plot,
		Rect priceRect,
		ChartPane price,
		List<(ChartPane Pane, IndicatorResultDto Result)> panes,
		int decimals,
		IReadOnlyList<BarDto> bars,
		double rightEdge
	)
	{
		var x =
			Math.Round(ChartCrosshairHelpers.GetSnappedX(p.X, rightEdge, plot.Right, _barSpacing))
			+ 0.5;
		var y = Math.Round(p.Y) + 0.5;
		if (x >= plot.X && x <= plot.Right)
			context.DrawLine(CrosshairModePen, new Point(x, plot.Y), new Point(x, plot.Bottom));
		context.DrawLine(CrosshairModePen, new Point(plot.X, y), new Point(plot.Right, y));

		// The value on the price axis: the price chart's, or the pane's under the pointer
		if (p.Y <= priceRect.Bottom)
			DrawAxisLabel(
				context,
				plot.Right,
				p.Y,
				price.Value(p.Y),
				decimals,
				CrosshairLabelBrush
			);
		else
			foreach (var (pane, result) in panes)
				if (pane.Contains(p.Y))
				{
					DrawAxisLabel(
						context,
						plot.Right,
						p.Y,
						pane.Value(p.Y),
						GetDecimals(result, pane),
						CrosshairLabelBrush
					);
					break;
				}

		// The bar's time on the time axis (a time a bar length on past the latest bar)
		var index = ChartViewportHelpers.GetBarIndexAt(p.X, rightEdge, plot.Right, _barSpacing);
		var time = ToShown(
			index >= 0 && index < bars.Count
				? bars[index].Time
				: ChartDrawingHelpers.GetTimeAt(bars, index, BarLength)
		);
		var text = Text(ChartCrosshairHelpers.FormatTime(time, IsIntraday), 11, LabelBrush);
		var width = text.Width + 12;
		var rect = new Rect(
			Math.Clamp(x - width / 2, plot.X, Math.Max(plot.X, plot.Right - width)),
			plot.Bottom + 1,
			width,
			TimeAxisHeight - 2
		);
		context.DrawRectangle(CrosshairLabelBrush, null, rect);
		context.DrawText(text, new Point(rect.X + 6, rect.Y + (rect.Height - text.Height) / 2));
	}
}
