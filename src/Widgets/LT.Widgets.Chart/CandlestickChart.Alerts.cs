using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using LT.Contracts.Dtos.PriceAlerts;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

/// <summary>
/// Price alerts on the price chart: a line at each level with a tag at the plot's left end
/// (direction, note, ✕) and the level on the price axis. Dragging the line or its tag moves the
/// alert (<see cref="AlertMoved"/>, at the price as shown on the axis); ✕ removes it
/// (<see cref="AlertRemoved"/>). Triggered alerts stay as a grey dotted line until removed. Alerts
/// outside the visible price range are not drawn.
/// <para>
/// While <see cref="IsPlacingAlert"/> (the toolbox's alert tool) a dashed preview line follows the
/// pointer's price on the price chart, with its level on the axis, and a left click there raises
/// <see cref="AlertPlaced"/> instead of panning or grabbing what is under it.
/// </para>
/// </summary>
public partial class CandlestickChart
{
	public static readonly StyledProperty<IReadOnlyList<PriceAlertDto>?> AlertsProperty =
		AvaloniaProperty.Register<CandlestickChart, IReadOnlyList<PriceAlertDto>?>(nameof(Alerts));

	public static readonly StyledProperty<bool> IsPlacingAlertProperty = AvaloniaProperty.Register<
		CandlestickChart,
		bool
	>(nameof(IsPlacingAlert));

	// Grab distance (DIPs) above and below an alert line
	private const double AlertHitHeight = 4;

	private static readonly ImmutableSolidColorBrush AlertBrush = new(Color.Parse("#E040FB"));
	private static readonly ImmutableSolidColorBrush AlertCloseBrush = new(Color.Parse("#B02FC7"));
	private static readonly ImmutableSolidColorBrush TriggeredAlertBrush = new(
		Color.Parse("#6B7385")
	);
	private static readonly IPen AlertPen = new ImmutablePen(AlertBrush, 1);
	private static readonly IPen TriggeredAlertPen = new ImmutablePen(
		TriggeredAlertBrush,
		1,
		new ImmutableDashStyle([1, 3], 0)
	);
	private static readonly IPen AlertPreviewPen = new ImmutablePen(
		AlertBrush,
		1,
		new ImmutableDashStyle([4, 3], 0)
	);
	private static readonly Cursor MoveVerticalCursor = new(StandardCursorType.SizeNorthSouth);
	private static readonly Cursor PlaceCursor = new(StandardCursorType.Cross);

	// The price scale as last drawn, for the price under a point
	private ChartPane? _pricePane;
	private int _priceDecimals = 2;

	// Alerts as last drawn: their line's y and tag, their ✕
	private readonly List<(double Y, Rect Tag, string Id)> _alertLines = [];
	private readonly List<(Rect Rect, string Id)> _alertCloseButtons = [];

	// The alert line being dragged and where it is now
	private string? _draggedAlert;
	private double _draggedAlertPrice;

	/// <summary>The charted contract's alerts.</summary>
	public IReadOnlyList<PriceAlertDto>? Alerts
	{
		get => GetValue(AlertsProperty);
		set => SetValue(AlertsProperty, value);
	}

	/// <summary>
	/// The alert tool is armed: the pointer's price is previewed as a line and a left click on the
	/// price chart places the alert (<see cref="AlertPlaced"/>).
	/// </summary>
	public bool IsPlacingAlert
	{
		get => GetValue(IsPlacingAlertProperty);
		set => SetValue(IsPlacingAlertProperty, value);
	}

	/// <summary>The price chart was clicked while placing an alert: the price, as on the axis.</summary>
	public event Action<double>? AlertPlaced;

	/// <summary>An alert line was dragged to a new level: its id and the level.</summary>
	public event Action<string, double>? AlertMoved;

	/// <summary>An alert's ✕ was clicked.</summary>
	public event Action<string>? AlertRemoved;

	/// <summary>Where the chart was last right-clicked (its menu opens there).</summary>
	public Point? MenuPoint { get; private set; }

	/// <summary>The price under the pointer, rounded as the axis shows it; null off the price chart.</summary>
	public double? PointerPrice => _pointer is { } p ? GetPriceAt(p) : null;

	/// <summary>
	/// The price at a point of the price chart, rounded to the decimals of its axis; null outside
	/// it (the volume bars, indicator panes, axes).
	/// </summary>
	public double? GetPriceAt(Point position) =>
		_pricePane is { } pane
		&& position.X >= pane.Rect.Left
		&& position.X <= pane.Rect.Right
		&& pane.Contains(position.Y)
			? Math.Round(pane.Value(position.Y), _priceDecimals)
			: null;

	/// <summary>The alert whose line, tag or ✕ is at the point; null when none.</summary>
	public PriceAlertDto? GetAlertAt(Point position) =>
		HitAlert(position) is { } id ? Alerts?.FirstOrDefault(x => x.Id == id) : null;

	private string? HitAlertClose(Point position)
	{
		foreach (var (rect, id) in _alertCloseButtons)
			if (rect.Contains(position))
				return id;
		return null;
	}

	private string? HitAlert(Point position)
	{
		if (HitAlertClose(position) is { } close)
			return close;
		// The last drawn is on top
		for (var i = _alertLines.Count - 1; i >= 0; i--)
		{
			var (y, tag, id) = _alertLines[i];
			if (
				tag.Contains(position)
				|| (
					Math.Abs(position.Y - y) <= AlertHitHeight
					&& position.X >= _plot.X
					&& position.X <= _plot.Right
				)
			)
				return id;
		}
		return null;
	}

	private void DrawAlerts(DrawingContext context, ChartPane price, int decimals)
	{
		_pricePane = price;
		_priceDecimals = decimals;
		_alertLines.Clear();
		_alertCloseButtons.Clear();
		if (Alerts is not { Count: > 0 } alerts)
			return;
		var rect = price.Rect;
		// Triggered first, so active lines draw over them
		foreach (var alert in alerts.OrderBy(x => !x.IsTriggered || x.Id == _draggedAlert))
		{
			var dragged = alert.Id == _draggedAlert;
			var level = dragged ? _draggedAlertPrice : alert.Price;
			var y = Math.Round(price.Y(level)) + 0.5;
			if (y < rect.Top || y > rect.Bottom)
				continue;
			var triggered = alert.IsTriggered && !dragged;
			var brush = triggered ? TriggeredAlertBrush : AlertBrush;
			context.DrawLine(
				triggered ? TriggeredAlertPen : AlertPen,
				new Point(rect.X, y),
				new Point(rect.Right, y)
			);

			// Tag at the plot's left end: direction (or "fired"), the note, then ✕
			var arrow = alert.Condition == PriceAlertCondition.CrossingUp ? "▲" : "▼";
			var text = triggered ? $"Alert {arrow} fired" : $"Alert {arrow}";
			if (!string.IsNullOrWhiteSpace(alert.Note))
				text += $" · {alert.Note.Trim()}";
			var label = Text(text, 11, Background);
			var close = Text("✕", 11, LabelBrush);
			var height = label.Height + 4;
			var closeWidth = height;
			var tagWidth = Math.Min(label.Width + 10, Math.Max(40, rect.Width / 2));
			var tag = new Rect(rect.X + 4, y - height / 2, tagWidth, height);
			var closeRect = new Rect(tag.Right, tag.Y, closeWidth, height);
			context.DrawRectangle(brush, null, tag);
			using (context.PushClip(tag.Deflate(new Thickness(5, 0))))
				context.DrawText(label, new Point(tag.X + 5, y - label.Height / 2));
			context.DrawRectangle(triggered ? GridPen.Brush : AlertCloseBrush, null, closeRect);
			context.DrawText(
				close,
				new Point(closeRect.X + (closeWidth - close.Width) / 2, y - close.Height / 2)
			);
			_alertLines.Add((y, tag, alert.Id));
			_alertCloseButtons.Add((closeRect, alert.Id));

			// The level on the price axis
			var value = Text(
				level.ToString("N" + decimals, System.Globalization.CultureInfo.CurrentCulture),
				11,
				triggered ? LabelBrush : Background
			);
			context.DrawRectangle(
				brush,
				null,
				new Rect(rect.Right, y - value.Height / 2 - 2, _priceAxisWidth, value.Height + 4)
			);
			context.DrawText(value, new Point(AxisLabelX(rect), y - value.Height / 2));
		}
	}

	// While placing: a dashed line at the pointer's price (as the axis rounds it), the direction it
	// would get from the last close, and the level on the axis
	private void DrawAlertPreview(DrawingContext context, double lastClose)
	{
		if (
			!IsPlacingAlert
			|| _pointer is not { } p
			|| _pricePane is not { } pane
			|| GetPriceAt(p) is not { } level
		)
			return;
		var rect = pane.Rect;
		var y = Math.Round(pane.Y(level)) + 0.5;
		if (y < rect.Top || y > rect.Bottom)
			return;
		context.DrawLine(AlertPreviewPen, new Point(rect.X, y), new Point(rect.Right, y));
		var arrow =
			PriceAlertHelpers.GetCondition(level, lastClose) == PriceAlertCondition.CrossingUp
				? "▲"
				: "▼";
		var label = Text($"Alert {arrow} · click to place", 11, AlertBrush);
		context.DrawText(label, new Point(rect.Right - label.Width - 6, y - label.Height - 2));
		var value = Text(
			level.ToString("N" + _priceDecimals, System.Globalization.CultureInfo.CurrentCulture),
			11,
			Background
		);
		context.DrawRectangle(
			AlertBrush,
			null,
			new Rect(rect.Right, y - value.Height / 2 - 2, _priceAxisWidth, value.Height + 4)
		);
		context.DrawText(value, new Point(AxisLabelX(rect), y - value.Height / 2));
	}

	// Left button while placing: the alert goes at the price under it (nothing off the price
	// chart); the click starts nothing else. True when handled
	private bool OnAlertPlacementPressed(PointerPressedEventArgs e, Point position)
	{
		if (!IsPlacingAlert || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			return false;
		e.Handled = true;
		Focus();
		if (GetPriceAt(position) is { } price && price > 0)
			AlertPlaced?.Invoke(price);
		return true;
	}

	// Left button on an alert: ✕ removes it, the line or tag starts dragging it. True when handled
	private bool OnAlertPressed(PointerPressedEventArgs e, Point position)
	{
		if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			return false;
		if (HitAlertClose(position) is { } close)
		{
			AlertRemoved?.Invoke(close);
			return true;
		}
		if (
			HitAlert(position) is not { } id
			|| Alerts?.FirstOrDefault(x => x.Id == id) is not { } alert
		)
			return false;
		_draggedAlert = id;
		_draggedAlertPrice = alert.Price;
		e.Pointer.Capture(this);
		InvalidateVisual();
		return true;
	}

	// While dragging: the level follows the pointer, kept on the price chart
	private void DragAlert(Point position)
	{
		if (_pricePane is not { } pane)
			return;
		var y = Math.Clamp(position.Y, pane.Rect.Top, pane.Rect.Bottom);
		var price = Math.Round(pane.Value(y), _priceDecimals);
		if (price > 0)
			_draggedAlertPrice = price;
	}

	// Dropped: moved when the level changed
	private void OnAlertReleased()
	{
		if (_draggedAlert is not { } id)
			return;
		_draggedAlert = null;
		if (
			Alerts?.FirstOrDefault(x => x.Id == id) is { } alert
			&& alert.Price != _draggedAlertPrice
		)
			AlertMoved?.Invoke(id, _draggedAlertPrice);
	}

	// Cursor over alerts: a hand on ✕, up / down on a line or tag (and while dragging one)
	private Cursor? GetAlertCursor(Point? position)
	{
		if (_draggedAlert != null)
			return MoveVerticalCursor;
		if (IsPlacingAlert)
			return position is { } over && GetPriceAt(over) != null ? PlaceCursor : null;
		if (position is not { } p || _dragStart != null || _marginDragStartX != null)
			return null;
		if (HitAlertClose(p) != null)
			return HandCursor;
		return HitAlert(p) != null ? MoveVerticalCursor : null;
	}
}
