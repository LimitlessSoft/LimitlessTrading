using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using LT.App.Helpers;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

/// <summary>
/// Working orders on the price chart (<see cref="OrderLines"/>): a line at each order's price,
/// green for a buy and red for a sell (dashed for a stop price), with a tag at the plot's right
/// end ("SELL 1 LMT 1.60") and the price on the axis. A line that can move drags up and down,
/// its price snapped to <see cref="OrderTickSize"/> and shown on the tag and axis as it goes;
/// dropping it at a new price raises <see cref="OrderMoved"/> (the view model asks to confirm;
/// nothing is sent from here), Esc or losing the pointer puts it back. A line that can't move
/// shows why in a tooltip. A pending change is drawn dotted at its new price. Orders outside the
/// visible price range are not drawn.
/// </summary>
public partial class CandlestickChart
{
	public static readonly StyledProperty<IReadOnlyList<ChartOrderLineDto>?> OrderLinesProperty =
		AvaloniaProperty.Register<CandlestickChart, IReadOnlyList<ChartOrderLineDto>?>(
			nameof(OrderLines)
		);

	public static readonly StyledProperty<double> OrderTickSizeProperty = AvaloniaProperty.Register<
		CandlestickChart,
		double
	>(nameof(OrderTickSize), 0.01);

	// Grab distance (DIPs) above and below an order line
	private const double OrderHitHeight = 4;

	private static readonly ImmutableSolidColorBrush BuyOrderBrush = new(Color.Parse("#26A69A"));
	private static readonly ImmutableSolidColorBrush SellOrderBrush = new(Color.Parse("#EF5350"));
	private static readonly IPen BuyOrderPen = new ImmutablePen(BuyOrderBrush, 1);
	private static readonly IPen SellOrderPen = new ImmutablePen(SellOrderBrush, 1);
	private static readonly IPen BuyStopPen = new ImmutablePen(
		BuyOrderBrush,
		1,
		new ImmutableDashStyle([5, 3], 0)
	);
	private static readonly IPen SellStopPen = new ImmutablePen(
		SellOrderBrush,
		1,
		new ImmutableDashStyle([5, 3], 0)
	);
	private static readonly IPen BuyPendingPen = new ImmutablePen(
		BuyOrderBrush,
		1,
		new ImmutableDashStyle([1, 2], 0)
	);
	private static readonly IPen SellPendingPen = new ImmutablePen(
		SellOrderBrush,
		1,
		new ImmutableDashStyle([1, 2], 0)
	);

	// Order lines as last drawn: their y, tag and line
	private readonly List<(double Y, Rect Tag, ChartOrderLineDto Line)> _orderLines = [];

	// The order line being dragged and its (snapped) price now
	private string? _draggedOrder;
	private double _draggedOrderPrice;

	// The reason shown in the tooltip, for the line under the pointer that can't move
	private string? _orderTip;

	/// <summary>The charted contract's working orders.</summary>
	public IReadOnlyList<ChartOrderLineDto>? OrderLines
	{
		get => GetValue(OrderLinesProperty);
		set => SetValue(OrderLinesProperty, value);
	}

	/// <summary>The contract's minimum tick: a dragged order line's price snaps to it.</summary>
	public double OrderTickSize
	{
		get => GetValue(OrderTickSizeProperty);
		set => SetValue(OrderTickSizeProperty, value);
	}

	/// <summary>An order line was dropped at a new price: the line's id and the price (on the tick).</summary>
	public event Action<string, double>? OrderMoved;

	/// <summary>Esc while dragging an order line: it goes back. False when none was being dragged.</summary>
	public bool CancelOrderDrag()
	{
		if (_draggedOrder == null)
			return false;
		_draggedOrder = null;
		InvalidateVisual();
		return true;
	}

	private ChartOrderLineDto? HitOrderLine(Point position)
	{
		// The last drawn is on top
		for (var i = _orderLines.Count - 1; i >= 0; i--)
		{
			var (y, tag, line) = _orderLines[i];
			if (
				tag.Contains(position)
				|| (
					Math.Abs(position.Y - y) <= OrderHitHeight
					&& position.X >= _plot.X
					&& position.X <= _plot.Right
				)
			)
				return line;
		}
		return null;
	}

	private void DrawOrderLines(DrawingContext context, ChartPane price, int decimals)
	{
		_orderLines.Clear();
		if (OrderLines is not { Count: > 0 } lines)
			return;
		var rect = price.Rect;
		// Price decimals: the axis's, or the tick's when it needs more (0.005)
		var shown = Math.Max(decimals, ChartOrderHelpers.GetDecimals(OrderTickSize));
		// The dragged line on top
		foreach (var line in lines.OrderBy(x => x.Id == _draggedOrder))
		{
			var dragged = line.Id == _draggedOrder;
			var level = dragged ? _draggedOrderPrice : line.Price;
			var y = Math.Round(price.Y(level)) + 0.5;
			if (y < rect.Top || y > rect.Bottom)
				continue;
			var brush = line.IsBuy ? BuyOrderBrush : SellOrderBrush;
			var pen =
				dragged || line.IsPending ? (line.IsBuy ? BuyPendingPen : SellPendingPen)
				: line.IsStopPrice ? (line.IsBuy ? BuyStopPen : SellStopPen)
				: line.IsBuy ? BuyOrderPen
				: SellOrderPen;
			context.DrawLine(pen, new Point(rect.X, y), new Point(rect.Right, y));

			// Tag at the plot's right end: side, what is left, type, price (the new one while
			// dragged or pending, after the old one)
			var kind = line.IsStopPrice ? $"{line.OrderType} stop" : line.OrderType;
			var priceText = level.ToString("N" + shown, CultureInfo.CurrentCulture);
			var from =
				dragged ? line.Price
				: line.PendingFromPrice is { } pending ? pending
				: (double?)null;
			var text =
				$"{line.Action} {Sensitive.Apply(line.Quantity.ToString("#,0.####", CultureInfo.CurrentCulture))} {kind} "
				+ (
					from is { } old && old != level
						? $"{old.ToString("N" + shown, CultureInfo.CurrentCulture)} → {priceText}"
						: priceText
				);
			if (line.IsPending && !dragged)
				text += " · pending";
			var label = Text(text, 11, Background);
			var height = label.Height + 4;
			var tagWidth = Math.Min(label.Width + 10, Math.Max(40, rect.Width / 2));
			var tag = new Rect(rect.Right - tagWidth - 4, y - height / 2, tagWidth, height);
			context.DrawRectangle(brush, null, tag);
			using (context.PushClip(tag.Deflate(new Thickness(5, 0))))
				context.DrawText(label, new Point(tag.X + 5, y - label.Height / 2));
			_orderLines.Add((y, tag, line));

			// The price on the axis
			var value = Text(priceText, 11, Background);
			context.DrawRectangle(
				brush,
				null,
				new Rect(rect.Right, y - value.Height / 2 - 2, _priceAxisWidth, value.Height + 4)
			);
			context.DrawText(value, new Point(AxisLabelX(rect), y - value.Height / 2));
		}
	}

	// Left button on a line that can move: starts dragging it. True when handled
	private bool OnOrderPressed(PointerPressedEventArgs e, Point position)
	{
		if (
			!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
			|| HitOrderLine(position) is not { CanMove: true, IsPending: false } line
		)
			return false;
		_draggedOrder = line.Id;
		_draggedOrderPrice = line.Price;
		SetOrderTip(null);
		e.Pointer.Capture(this);
		InvalidateVisual();
		return true;
	}

	// While dragging: the price follows the pointer on the tick, kept on the price chart
	private void DragOrder(Point position)
	{
		if (_pricePane is not { } pane)
			return;
		var y = Math.Clamp(position.Y, pane.Rect.Top, pane.Rect.Bottom);
		var price = ChartOrderHelpers.SnapToTick(pane.Value(y), OrderTickSize);
		if (price > 0)
			_draggedOrderPrice = price;
	}

	// Dropped: asks for the change when the price moved
	private void OnOrderReleased()
	{
		if (_draggedOrder is not { } id)
			return;
		_draggedOrder = null;
		if (
			OrderLines?.FirstOrDefault(x => x.Id == id) is { } line
			&& line.Price != _draggedOrderPrice
		)
			OrderMoved?.Invoke(id, _draggedOrderPrice);
	}

	// Cursor over order lines: up / down on one that moves (and while dragging); a line that
	// can't move shows why in the tooltip
	private Cursor? GetOrderCursor(Point? position)
	{
		if (_draggedOrder != null)
			return MoveVerticalCursor;
		var line =
			position is { } p
			&& _dragStart == null
			&& _marginDragStartX == null
			&& _draggedAlert == null
			&& !IsPlacingAlert
				? HitOrderLine(p)
				: null;
		SetOrderTip(line is { CanMove: false } blocked ? blocked.MoveBlockedReason : null);
		return line is { CanMove: true, IsPending: false } ? MoveVerticalCursor : null;
	}

	private void SetOrderTip(string? reason)
	{
		if (reason == _orderTip)
			return;
		_orderTip = reason;
		ToolTip.SetTip(this, reason);
		ToolTip.SetIsOpen(this, reason != null);
	}
}
