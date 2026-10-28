using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

/// <summary>
/// Drawings on the price chart (<see cref="Drawings"/>): anchored at a time and a price, so they
/// follow pan, zoom, resize and new bars (geometry and hit tests in ChartDrawingHelpers).
/// <para>
/// Only in drawing mode (<see cref="IsDrawingMode"/>) do they take the pointer. With a tool armed
/// (<see cref="DrawingTool"/>) the clicks on the price chart place its points — or a press, drag
/// and release places two — with a preview following the pointer, and the finished points are
/// raised as <see cref="DrawingCreated"/>; a stroke is drawn while the button is held. With the
/// cursor (no tool) a click selects the drawing under it (<see cref="SelectedDrawingId"/>),
/// dragging moves it or one of its anchors (<see cref="DrawingEdited"/> on release), and a
/// double-click on a note asks to edit it (<see cref="DrawingTextEditRequested"/>); a click
/// elsewhere drops the selection and pans as usual. Locked drawings can't be selected or moved;
/// hidden ones are neither drawn nor hit. Every point but a stroke's sits on a bar's centre.
/// </para>
/// </summary>
public partial class CandlestickChart
{
	public static readonly StyledProperty<IReadOnlyList<ChartDrawingDto>?> DrawingsProperty =
		AvaloniaProperty.Register<CandlestickChart, IReadOnlyList<ChartDrawingDto>?>(
			nameof(Drawings)
		);

	public static readonly StyledProperty<bool> IsDrawingModeProperty = AvaloniaProperty.Register<
		CandlestickChart,
		bool
	>(nameof(IsDrawingMode));

	public static readonly StyledProperty<ChartDrawingKind?> DrawingToolProperty =
		AvaloniaProperty.Register<CandlestickChart, ChartDrawingKind?>(nameof(DrawingTool));

	public static readonly StyledProperty<string?> SelectedDrawingIdProperty =
		AvaloniaProperty.Register<CandlestickChart, string?>(
			nameof(SelectedDrawingId),
			defaultBindingMode: BindingMode.TwoWay
		);

	public static readonly StyledProperty<bool> DrawingsLockedProperty = AvaloniaProperty.Register<
		CandlestickChart,
		bool
	>(nameof(DrawingsLocked));

	public static readonly StyledProperty<bool> DrawingsHiddenProperty = AvaloniaProperty.Register<
		CandlestickChart,
		bool
	>(nameof(DrawingsHidden));

	private const double AnchorRadius = 4;
	private const double ArrowHeadSize = 10;
	private const double TextMaxWidth = 320;

	private static readonly ImmutableSolidColorBrush DrawingBrush = new(Color.Parse("#2962FF"));
	private static readonly ImmutableSolidColorBrush DrawingFillBrush = new(
		Color.Parse("#262962FF")
	);
	private static readonly ImmutableSolidColorBrush DrawingTextBrush = new(Color.Parse("#E6E8EB"));
	private static readonly ImmutableSolidColorBrush MeasureUpFill = new(Color.Parse("#3326A69A"));
	private static readonly ImmutableSolidColorBrush MeasureDownFill = new(
		Color.Parse("#33EF5350")
	);
	private static readonly IPen DrawingPen = new ImmutablePen(DrawingBrush, 1.5);
	private static readonly IPen DrawingGuidePen = new ImmutablePen(
		DrawingBrush,
		1,
		new ImmutableDashStyle([4, 3], 0)
	);
	private static readonly IPen AnchorPen = new ImmutablePen(DrawingBrush, 1.5);
	private static readonly IPen TextOutlinePen = new ImmutablePen(
		DrawingBrush,
		1,
		new ImmutableDashStyle([2, 2], 0)
	);

	// Fib levels' colours, by level index (TradingView-like)
	private static readonly ImmutableSolidColorBrush[] FibBrushes =
	[
		new(Color.Parse("#787B86")),
		new(Color.Parse("#F23645")),
		new(Color.Parse("#FF9800")),
		new(Color.Parse("#4CAF50")),
		new(Color.Parse("#089981")),
		new(Color.Parse("#00BCD4")),
		new(Color.Parse("#787B86")),
		new(Color.Parse("#2962FF")),
	];

	private static readonly Cursor MoveCursor = new(StandardCursorType.SizeAll);

	// The bar position of the plot's right edge as last drawn (with _plot, _barSpacing and
	// _pricePane it maps chart points to the screen and back)
	private double _rightEdge;

	// Drawings as last drawn, in drawing order: their anchors on screen and a note's box
	private readonly List<DrawnDrawing> _drawn = [];

	// A drawing being placed: its points so far, the press that may drag out the next one, and
	// a stroke's screen points while the button is held
	private readonly List<ChartDrawingPointDto> _newPoints = [];
	private Point? _newPressAt;
	private List<(double X, double Y)>? _stroke;

	// A drawing being moved (anchor −1: the whole of it)
	private DrawingEdit? _edit;

	private sealed record DrawnDrawing(
		ChartDrawingDto Drawing,
		(double X, double Y)[] Points,
		Rect? TextBox
	);

	private sealed class DrawingEdit
	{
		public required ChartDrawingDto Drawing { get; init; }
		public required int Anchor { get; init; }
		public required Point PressedAt { get; init; }
		public required double StartPosition { get; init; }
		public required double StartPrice { get; init; }
		public required (double Position, double Price)[] Original { get; init; }
		public required List<ChartDrawingPointDto> Current { get; set; }
		public bool Moved { get; set; }
	}

	/// <summary>The charted contract's drawings, oldest first (the newest on top).</summary>
	public IReadOnlyList<ChartDrawingDto>? Drawings
	{
		get => GetValue(DrawingsProperty);
		set => SetValue(DrawingsProperty, value);
	}

	/// <summary>Drawing mode: drawings take the pointer (select, move, draw new ones).</summary>
	public bool IsDrawingMode
	{
		get => GetValue(IsDrawingModeProperty);
		set => SetValue(IsDrawingModeProperty, value);
	}

	/// <summary>The armed drawing tool; null is the cursor.</summary>
	public ChartDrawingKind? DrawingTool
	{
		get => GetValue(DrawingToolProperty);
		set => SetValue(DrawingToolProperty, value);
	}

	/// <summary>The selected drawing (its anchors shown); set by the chart on a click.</summary>
	public string? SelectedDrawingId
	{
		get => GetValue(SelectedDrawingIdProperty);
		set => SetValue(SelectedDrawingIdProperty, value);
	}

	public bool DrawingsLocked
	{
		get => GetValue(DrawingsLockedProperty);
		set => SetValue(DrawingsLockedProperty, value);
	}

	public bool DrawingsHidden
	{
		get => GetValue(DrawingsHiddenProperty);
		set => SetValue(DrawingsHiddenProperty, value);
	}

	/// <summary>A drawing was finished: its kind and points.</summary>
	public event Action<ChartDrawingKind, IReadOnlyList<ChartDrawingPointDto>>? DrawingCreated;

	/// <summary>A drawing (or one of its anchors) was dragged: its id and new points.</summary>
	public event Action<string, IReadOnlyList<ChartDrawingPointDto>>? DrawingEdited;

	/// <summary>A note was double-clicked with the cursor: its id.</summary>
	public event Action<string>? DrawingTextEditRequested;

	private bool IsDrawingToolArmed => IsDrawingMode && DrawingTool != null;

	// Drawings take the pointer: drawing mode, the cursor, not locked or hidden
	private bool CanEditDrawings =>
		IsDrawingMode && DrawingTool == null && !DrawingsLocked && !DrawingsHidden;

	private TimeSpan BarLength => ChartTimeframeHelpers.GetBarLength(Timeframe);

	/// <summary>
	/// Esc: drops a drawing being placed or moved (a moved one goes back). False when there was
	/// none.
	/// </summary>
	public bool CancelDrawing()
	{
		if (_edit != null)
		{
			_edit = null;
			InvalidateVisual();
			return true;
		}
		if (_newPoints.Count == 0 && _stroke == null)
			return false;
		ResetDrawingInput();
		return true;
	}

	private void ResetDrawingInput()
	{
		_newPoints.Clear();
		_newPressAt = null;
		_stroke = null;
		InvalidateVisual();
	}

	private void OnDrawingPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		if (change.Property == DrawingToolProperty || change.Property == IsDrawingModeProperty)
		{
			ResetDrawingInput();
			_edit = null;
			UpdateHandleHover(_pointer);
		}
		else if (
			change.Property == DrawingsLockedProperty
			|| change.Property == DrawingsHiddenProperty
		)
		{
			_edit = null;
			UpdateHandleHover(_pointer);
		}
	}

	// ---- mapping -------------------------------------------------------------------------

	private bool CanMapDrawings => Bars is { Count: > 0 } && _pricePane != null;

	private (double X, double Y) ToScreen(ChartDrawingPointDto point) =>
		(
			ChartViewportHelpers.GetPositionX(
				ChartDrawingHelpers.GetBarPosition(Bars!, point.Time, BarLength),
				_rightEdge,
				_plot.Right,
				_barSpacing
			),
			_pricePane!.Value.Y(point.Price)
		);

	private double GetPositionAt(double x) =>
		ChartViewportHelpers.GetPositionAt(x, _rightEdge, _plot.Right, _barSpacing);

	// The chart point under a screen point, its y kept on the price chart; on a bar's centre
	// when the kind snaps
	private ChartDrawingPointDto ToChartPoint(Point position, bool snap)
	{
		var pane = _pricePane!.Value;
		var barPosition = GetPositionAt(position.X);
		if (snap)
			barPosition = Math.Floor(barPosition + 0.5);
		return new ChartDrawingPointDto
		{
			Time = ChartDrawingHelpers.GetTimeAt(Bars!, barPosition, BarLength),
			Price = pane.Value(Math.Clamp(position.Y, pane.Rect.Top, pane.Rect.Bottom)),
		};
	}

	private bool IsOnPriceChart(Point position) =>
		_pricePane is { } pane
		&& position.X >= pane.Rect.Left
		&& position.X <= pane.Rect.Right
		&& pane.Contains(position.Y);

	// ---- rendering -----------------------------------------------------------------------

	private void DrawDrawings(DrawingContext context, Rect plot, int decimals)
	{
		_drawn.Clear();
		if (!CanMapDrawings)
			return;
		if (!DrawingsHidden)
			foreach (var drawing in Drawings ?? [])
			{
				var points = _edit?.Drawing.Id == drawing.Id ? _edit.Current : drawing.Points;
				if (!ChartDrawingHelpers.HasValidPointCount(drawing.Kind, points.Count))
					continue;
				var screen = points.Select(ToScreen).ToArray();
				var box = DrawDrawing(
					context,
					plot,
					drawing.Kind,
					points,
					screen,
					drawing.Text,
					decimals
				);
				_drawn.Add(new DrawnDrawing(drawing, screen, box));
			}

		DrawNewDrawing(context, plot, decimals);

		// The selected one's anchors, over everything
		if (
			IsDrawingMode
			&& SelectedDrawingId is { } id
			&& _drawn.FirstOrDefault(x => x.Drawing.Id == id) is { } selected
		)
		{
			if (selected.TextBox is { } textBox)
				context.DrawRectangle(null, TextOutlinePen, textBox.Inflate(2));
			foreach (
				var index in ChartDrawingHelpers.GetEditableAnchors(
					selected.Drawing.Kind,
					selected.Points.Length
				)
			)
			{
				var (x, y) = selected.Points[index];
				context.DrawEllipse(
					Background,
					AnchorPen,
					new Point(x, y),
					AnchorRadius,
					AnchorRadius
				);
			}
		}
	}

	// The drawing being placed: its points so far and the pointer as the next one
	private void DrawNewDrawing(DrawingContext context, Rect plot, int decimals)
	{
		if (!IsDrawingToolArmed || DrawingTool is not { } kind)
			return;
		if (_stroke is { Count: > 1 } stroke)
		{
			DrawStroke(context, stroke);
			return;
		}
		if (_newPoints.Count == 0 || _pointer is not { } pointer)
			return;
		var points = new List<ChartDrawingPointDto>(_newPoints)
		{
			ToChartPoint(pointer, ChartDrawingHelpers.SnapsToBars(kind)),
		};
		var screen = points.Select(ToScreen).ToArray();
		DrawDrawing(context, plot, kind, points, screen, null, decimals);
		foreach (var (x, y) in screen)
			context.DrawEllipse(Background, AnchorPen, new Point(x, y), AnchorRadius, AnchorRadius);
	}

	// One drawing (also a preview with fewer points than it takes); a note's box when it is one
	private Rect? DrawDrawing(
		DrawingContext context,
		Rect plot,
		ChartDrawingKind kind,
		IReadOnlyList<ChartDrawingPointDto> points,
		(double X, double Y)[] screen,
		string? text,
		int decimals
	)
	{
		var priceRect = _pricePane!.Value.Rect;
		var bounds = (priceRect.Left, priceRect.Top, priceRect.Right, priceRect.Bottom);
		static Point P((double X, double Y) p) => new(p.X, p.Y);
		var a = screen[0];

		if (kind == ChartDrawingKind.VerticalLine)
		{
			var x = Math.Round(a.X) + 0.5;
			if (x < plot.Left || x > plot.Right)
				return null;
			context.DrawLine(DrawingPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
			var label = Text(FormatTime(ToShown(points[0].Time), null), 11, Background);
			var tag = new Rect(
				x - label.Width / 2 - 4,
				plot.Bottom + 2,
				label.Width + 8,
				label.Height + 2
			);
			context.DrawRectangle(DrawingBrush, null, tag);
			context.DrawText(label, new Point(tag.X + 4, tag.Y + 1));
			return null;
		}

		// A horizontal line's price on the axis, outside the plot's clip
		if (kind is ChartDrawingKind.HorizontalLine or ChartDrawingKind.HorizontalRay)
			DrawDrawingPrice(context, priceRect, a.Y, points[0].Price, decimals);

		using (context.PushClip(priceRect))
		{
			switch (kind)
			{
				case ChartDrawingKind.HorizontalLine:
				case ChartDrawingKind.HorizontalRay:
				{
					var y = Math.Round(a.Y) + 0.5;
					var from = kind == ChartDrawingKind.HorizontalLine ? priceRect.Left : a.X;
					context.DrawLine(DrawingPen, new Point(from, y), new Point(priceRect.Right, y));
					break;
				}
				case ChartDrawingKind.Text:
				{
					var label = Text(text ?? "", 13, DrawingTextBrush);
					label.MaxTextWidth = TextMaxWidth;
					var origin = new Point(a.X, a.Y - label.Height / 2);
					context.DrawText(label, origin);
					return new Rect(origin, new Size(Math.Max(8, label.Width), label.Height));
				}
				case ChartDrawingKind.Brush:
					DrawStroke(context, screen);
					break;
			}
			if (screen.Length < 2)
				return null;
			var b = screen[1];
			switch (kind)
			{
				case ChartDrawingKind.TrendLine:
				case ChartDrawingKind.Ray:
				case ChartDrawingKind.ExtendedLine:
				case ChartDrawingKind.Arrow:
				{
					if (ChartDrawingHelpers.GetLine(kind, a, b, bounds) is { } line)
						context.DrawLine(DrawingPen, P(line.Start), P(line.End));
					if (kind == ChartDrawingKind.Arrow)
					{
						var (left, right) = ChartDrawingHelpers.GetArrowHead(a, b, ArrowHeadSize);
						DrawPolygon(context, DrawingBrush, DrawingPen, [b, left, right]);
					}
					break;
				}
				case ChartDrawingKind.Rectangle:
					context.DrawRectangle(DrawingFillBrush, DrawingPen, Box(a, b));
					break;
				case ChartDrawingKind.Ellipse:
					context.DrawEllipse(
						DrawingFillBrush,
						DrawingPen,
						new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2),
						Math.Abs(b.X - a.X) / 2,
						Math.Abs(b.Y - a.Y) / 2
					);
					break;
				case ChartDrawingKind.ParallelChannel:
					if (screen.Length < 3)
					{
						context.DrawLine(DrawingPen, P(a), P(b));
						break;
					}
					DrawChannel(context, a, b, screen[2]);
					break;
				case ChartDrawingKind.Triangle:
					if (screen.Length < 3)
						context.DrawLine(DrawingPen, P(a), P(b));
					else
						DrawPolygon(context, DrawingFillBrush, DrawingPen, [a, b, screen[2]]);
					break;
				case ChartDrawingKind.FibRetracement:
					DrawFibRetracement(context, points, a, b);
					break;
				case ChartDrawingKind.FibExtension:
					if (screen.Length < 3)
						context.DrawLine(DrawingGuidePen, P(a), P(b));
					else
						DrawFibExtension(context, points, a, b, screen[2]);
					break;
				case ChartDrawingKind.PriceRange:
					DrawPriceRange(context, points, a, b, decimals);
					break;
				case ChartDrawingKind.DateRange:
					DrawDateRange(context, points, a, b);
					break;
			}
		}

		return null;
	}

	private void DrawDrawingPrice(
		DrawingContext context,
		Rect priceRect,
		double y,
		double price,
		int decimals
	)
	{
		y = Math.Round(y) + 0.5;
		if (y < priceRect.Top || y > priceRect.Bottom)
			return;
		var value = Text(
			price.ToString("N" + decimals, CultureInfo.CurrentCulture),
			11,
			LabelBrush
		);
		context.DrawRectangle(
			DrawingBrush,
			null,
			new Rect(priceRect.Right, y - value.Height / 2 - 2, _priceAxisWidth, value.Height + 4)
		);
		context.DrawText(value, new Point(AxisLabelX(priceRect), y - value.Height / 2));
	}

	// The box between two opposite corners, whichever way round
	private static Rect Box((double X, double Y) a, (double X, double Y) b) =>
		new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));

	private static void DrawPolygon(
		DrawingContext context,
		IBrush? fill,
		IPen? pen,
		IReadOnlyList<(double X, double Y)> corners
	)
	{
		var geometry = new StreamGeometry();
		using (var g = geometry.Open())
		{
			g.BeginFigure(new Point(corners[0].X, corners[0].Y), fill != null);
			for (var i = 1; i < corners.Count; i++)
				g.LineTo(new Point(corners[i].X, corners[i].Y));
			g.EndFigure(true);
		}
		context.DrawGeometry(fill, pen, geometry);
	}

	private static void DrawStroke(
		DrawingContext context,
		IReadOnlyList<(double X, double Y)> points
	)
	{
		if (points.Count < 2)
			return;
		var geometry = new StreamGeometry();
		using (var g = geometry.Open())
		{
			g.BeginFigure(new Point(points[0].X, points[0].Y), false);
			for (var i = 1; i < points.Count; i++)
				g.LineTo(new Point(points[i].X, points[i].Y));
			g.EndFigure(false);
		}
		context.DrawGeometry(null, DrawingPen, geometry);
	}

	private static void DrawChannel(
		DrawingContext context,
		(double X, double Y) a,
		(double X, double Y) b,
		(double X, double Y) c
	)
	{
		var offset = ChartDrawingHelpers.GetChannelOffset(a, b, c);
		(double X, double Y) a2 = (a.X, a.Y + offset);
		(double X, double Y) b2 = (b.X, b.Y + offset);
		// Filled between its two lines; no edges at the ends
		DrawPolygon(context, DrawingFillBrush, null, [a, b, b2, a2]);
		context.DrawLine(DrawingPen, new Point(a.X, a.Y), new Point(b.X, b.Y));
		context.DrawLine(DrawingPen, new Point(a2.X, a2.Y), new Point(b2.X, b2.Y));
		context.DrawLine(
			DrawingGuidePen,
			new Point(a.X, a.Y + offset / 2),
			new Point(b.X, b.Y + offset / 2)
		);
	}

	private void DrawFibRetracement(
		DrawingContext context,
		IReadOnlyList<ChartDrawingPointDto> points,
		(double X, double Y) a,
		(double X, double Y) b
	)
	{
		var left = Math.Min(a.X, b.X);
		var right = Math.Max(a.X, b.X);
		context.DrawLine(DrawingGuidePen, new Point(a.X, a.Y), new Point(b.X, b.Y));
		var levels = ChartDrawingConstants.FibRetracementLevels;
		DrawFibLevels(
			context,
			levels,
			level => ChartDrawingHelpers.GetFibRetracementPrice(a.Y, b.Y, level),
			level =>
				ChartDrawingHelpers.GetFibRetracementPrice(points[0].Price, points[1].Price, level),
			left,
			right
		);
	}

	private void DrawFibExtension(
		DrawingContext context,
		IReadOnlyList<ChartDrawingPointDto> points,
		(double X, double Y) a,
		(double X, double Y) b,
		(double X, double Y) c
	)
	{
		context.DrawLine(DrawingGuidePen, new Point(a.X, a.Y), new Point(b.X, b.Y));
		context.DrawLine(DrawingGuidePen, new Point(b.X, b.Y), new Point(c.X, c.Y));
		var (left, right) = ChartDrawingHelpers.GetFibExtensionSpan(a, b, c);
		DrawFibLevels(
			context,
			ChartDrawingConstants.FibExtensionLevels,
			level => ChartDrawingHelpers.GetFibExtensionPrice(a.Y, b.Y, c.Y, level),
			level =>
				ChartDrawingHelpers.GetFibExtensionPrice(
					points[0].Price,
					points[1].Price,
					points[2].Price,
					level
				),
			left,
			right
		);
	}

	// Each level's line, a faint band to the next, and its label ("0.618 (123.45)") at the left
	private void DrawFibLevels(
		DrawingContext context,
		IReadOnlyList<double> levels,
		Func<double, double> y,
		Func<double, double> price,
		double left,
		double right
	)
	{
		for (var i = 0; i < levels.Count; i++)
		{
			var brush = FibBrushes[i % FibBrushes.Length];
			var levelY = y(levels[i]);
			if (i + 1 < levels.Count)
			{
				var nextY = y(levels[i + 1]);
				context.DrawRectangle(
					new ImmutableSolidColorBrush(brush.Color, 0.08),
					null,
					new Rect(left, Math.Min(levelY, nextY), right - left, Math.Abs(nextY - levelY))
				);
			}
			var lineY = Math.Round(levelY) + 0.5;
			context.DrawLine(
				new ImmutablePen(brush, 1),
				new Point(left, lineY),
				new Point(right, lineY)
			);
			var label = Text(
				$"{levels[i].ToString("0.###", CultureInfo.CurrentCulture)} ({price(levels[i]).ToString("N" + _priceDecimals, CultureInfo.CurrentCulture)})",
				10,
				brush
			);
			// Kept on the plot when the levels start left of it
			context.DrawText(
				label,
				new Point(Math.Max(left, _plot.Left) + 2, lineY - label.Height)
			);
		}
	}

	private void DrawPriceRange(
		DrawingContext context,
		IReadOnlyList<ChartDrawingPointDto> points,
		(double X, double Y) a,
		(double X, double Y) b,
		int decimals
	)
	{
		var up = points[1].Price >= points[0].Price;
		context.DrawRectangle(up ? MeasureUpFill : MeasureDownFill, null, Box(a, b));
		var midX = (a.X + b.X) / 2;
		var pen = new ImmutablePen(up ? UpBrush : DownBrush, 1.5);
		context.DrawLine(pen, new Point(midX, a.Y), new Point(midX, b.Y));
		var (left, right) = ChartDrawingHelpers.GetArrowHead((midX, a.Y), (midX, b.Y), 8);
		context.DrawLine(pen, new Point(midX, b.Y), new Point(left.X, left.Y));
		context.DrawLine(pen, new Point(midX, b.Y), new Point(right.X, right.Y));
		DrawMeasureLabel(
			context,
			ChartDrawingHelpers.GetPriceRangeLabel(points[0].Price, points[1].Price, decimals),
			midX,
			up ? Math.Min(a.Y, b.Y) : Math.Max(a.Y, b.Y),
			up,
			up ? UpBrush : DownBrush
		);
	}

	private void DrawDateRange(
		DrawingContext context,
		IReadOnlyList<ChartDrawingPointDto> points,
		(double X, double Y) a,
		(double X, double Y) b
	)
	{
		context.DrawRectangle(DrawingFillBrush, null, Box(a, b));
		var midY = (a.Y + b.Y) / 2;
		context.DrawLine(DrawingPen, new Point(a.X, midY), new Point(b.X, midY));
		var (left, right) = ChartDrawingHelpers.GetArrowHead((a.X, midY), (b.X, midY), 8);
		context.DrawLine(DrawingPen, new Point(b.X, midY), new Point(left.X, left.Y));
		context.DrawLine(DrawingPen, new Point(b.X, midY), new Point(right.X, right.Y));
		DrawMeasureLabel(
			context,
			ChartDrawingHelpers.GetDateRangeLabel(
				GetPositionAt(a.X),
				GetPositionAt(b.X),
				points[0].Time,
				points[1].Time
			),
			(a.X + b.X) / 2,
			Math.Max(a.Y, b.Y),
			false,
			DrawingBrush
		);
	}

	// A measure's label in a filled tag above (or below) its box
	private static void DrawMeasureLabel(
		DrawingContext context,
		string text,
		double centreX,
		double edgeY,
		bool above,
		IBrush brush
	)
	{
		var label = Text(text, 11, LabelBrush);
		var width = label.Width + 10;
		var height = label.Height + 4;
		var tag = new Rect(
			centreX - width / 2,
			above ? edgeY - height - 4 : edgeY + 4,
			width,
			height
		);
		context.DrawRectangle(brush, null, tag, 3, 3);
		context.DrawText(label, new Point(tag.X + 5, tag.Y + 2));
	}

	// ---- pointer -------------------------------------------------------------------------

	// Left button with a drawing tool armed: places a point (a one-point tool is done at once),
	// or starts a stroke. True when handled (nothing else starts)
	private bool OnDrawingToolPressed(PointerPressedEventArgs e, Point position)
	{
		if (!IsDrawingToolArmed || DrawingTool is not { } kind || !CanMapDrawings)
			return false;
		if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			return false;
		e.Handled = true;
		// The first point on the price chart; the next ones may be off it (kept on its edge)
		if (_newPoints.Count == 0 && !IsOnPriceChart(position))
			return true;
		if (kind == ChartDrawingKind.Brush)
		{
			_stroke = [(position.X, position.Y)];
			e.Pointer.Capture(this);
			return true;
		}
		_newPoints.Add(ToChartPoint(position, ChartDrawingHelpers.SnapsToBars(kind)));
		if (_newPoints.Count >= Math.Max(1, ChartDrawingHelpers.GetPointCount(kind)))
		{
			FinishDrawing(kind);
			return true;
		}
		_newPressAt = position;
		e.Pointer.Capture(this);
		InvalidateVisual();
		return true;
	}

	// Left button with the cursor: an anchor of the selected drawing, or any drawing (the newest
	// on top), selects it and starts moving it. Off them the selection is dropped and the chart
	// pans. True when a drawing was hit
	private bool OnDrawingPressed(PointerPressedEventArgs e, Point position)
	{
		if (!IsDrawingMode || DrawingTool != null)
			return false;
		if (
			!CanEditDrawings
			|| !CanMapDrawings
			|| !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
		)
			return false;
		if (HitDrawing(position) is not { } hit)
		{
			SelectedDrawingId = null;
			return false;
		}
		var (drawn, anchor) = hit;
		SelectedDrawingId = drawn.Drawing.Id;
		if (e.ClickCount == 2 && drawn.Drawing.Kind == ChartDrawingKind.Text)
		{
			DrawingTextEditRequested?.Invoke(drawn.Drawing.Id);
			return true;
		}
		var pane = _pricePane!.Value;
		_edit = new DrawingEdit
		{
			Drawing = drawn.Drawing,
			Anchor = anchor,
			PressedAt = position,
			StartPosition = GetPositionAt(position.X),
			StartPrice = pane.Value(position.Y),
			Original =
			[
				.. drawn.Drawing.Points.Select(x =>
					(ChartDrawingHelpers.GetBarPosition(Bars!, x.Time, BarLength), x.Price)
				),
			],
			Current = [.. drawn.Drawing.Points],
		};
		e.Pointer.Capture(this);
		InvalidateVisual();
		return true;
	}

	// The drawing under a point and its anchor there (−1: its body)
	private (DrawnDrawing Drawn, int Anchor)? HitDrawing(Point position)
	{
		var point = (position.X, position.Y);
		var tolerance = ChartDrawingConstants.HitTolerance;
		if (
			SelectedDrawingId is { } id
			&& _drawn.FirstOrDefault(x => x.Drawing.Id == id) is { } selected
		)
		{
			var editable = ChartDrawingHelpers.GetEditableAnchors(
				selected.Drawing.Kind,
				selected.Points.Length
			);
			var anchor = ChartDrawingHelpers.HitAnchor(
				[.. editable.Select(i => selected.Points[i])],
				point,
				tolerance + 1
			);
			if (anchor >= 0)
				return (selected, editable[anchor]);
		}
		var rect = _pricePane!.Value.Rect;
		var bounds = (rect.Left, rect.Top, rect.Right, rect.Bottom);
		var plotBounds = (_plot.Left, _plot.Top, _plot.Right, _plot.Bottom);
		for (var i = _drawn.Count - 1; i >= 0; i--)
		{
			var drawn = _drawn[i];
			var kind = drawn.Drawing.Kind;
			if (
				drawn.TextBox is { } box
					? box.Inflate(2).Contains(position)
					: ChartDrawingHelpers.HitTest(
						kind,
						drawn.Points,
						point,
						kind == ChartDrawingKind.VerticalLine ? plotBounds : bounds,
						tolerance
					)
			)
				return (drawn, -1);
		}
		return null;
	}

	// While the pointer moves: a stroke grows, a moved drawing follows
	private void OnDrawingPointerMoved(Point position)
	{
		if (_stroke is { } stroke)
		{
			var last = stroke[^1];
			var dx = position.X - last.X;
			var dy = position.Y - last.Y;
			if (
				dx * dx + dy * dy
					>= ChartDrawingConstants.BrushMinStep * ChartDrawingConstants.BrushMinStep
				&& stroke.Count < ChartDrawingConstants.MaxBrushPoints
			)
				stroke.Add((position.X, position.Y));
			return;
		}
		if (_edit is not { } edit || !CanMapDrawings)
			return;
		if (
			!edit.Moved
			&& Math.Abs(position.X - edit.PressedAt.X) < ChartDrawingConstants.DragThreshold
			&& Math.Abs(position.Y - edit.PressedAt.Y) < ChartDrawingConstants.DragThreshold
		)
			return;
		edit.Moved = true;
		var snap = ChartDrawingHelpers.SnapsToBars(edit.Drawing.Kind);
		if (edit.Anchor >= 0)
		{
			var current = new List<ChartDrawingPointDto>(edit.Current);
			current[edit.Anchor] = ToChartPoint(position, snap);
			edit.Current = current;
			return;
		}
		var pane = _pricePane!.Value;
		var shift = GetPositionAt(position.X) - edit.StartPosition;
		if (snap)
			shift = Math.Round(shift);
		var priceShift =
			pane.Value(Math.Clamp(position.Y, pane.Rect.Top, pane.Rect.Bottom)) - edit.StartPrice;
		edit.Current =
		[
			.. edit.Original.Select(x => new ChartDrawingPointDto
			{
				Time = ChartDrawingHelpers.GetTimeAt(Bars!, x.Position + shift, BarLength),
				Price = x.Price + priceShift,
			}),
		];
	}

	// Released: a stroke or a dragged-out drawing is finished, a moved drawing is dropped
	private void OnDrawingReleased(Point position)
	{
		if (_stroke is { } stroke)
		{
			_stroke = null;
			if (DrawingTool is { } kind && CanMapDrawings)
			{
				var kept = ChartDrawingHelpers.SimplifyStroke(
					stroke,
					ChartDrawingConstants.BrushMinStep
				);
				if (kept.Count >= 2)
				{
					_newPoints.AddRange(kept.Select(x => ToChartPoint(new Point(x.X, x.Y), false)));
					FinishDrawing(kind);
				}
			}
			return;
		}
		if (_newPressAt is { } pressedAt)
		{
			_newPressAt = null;
			// Dragged out: the release is the next point
			if (
				DrawingTool is { } kind
				&& CanMapDrawings
				&& _newPoints.Count == 1
				&& (
					Math.Abs(position.X - pressedAt.X) >= ChartDrawingConstants.DragThreshold
					|| Math.Abs(position.Y - pressedAt.Y) >= ChartDrawingConstants.DragThreshold
				)
			)
			{
				_newPoints.Add(ToChartPoint(position, ChartDrawingHelpers.SnapsToBars(kind)));
				if (_newPoints.Count >= ChartDrawingHelpers.GetPointCount(kind))
					FinishDrawing(kind);
			}
			return;
		}
		if (_edit is not { } edit)
			return;
		_edit = null;
		if (edit.Moved)
			DrawingEdited?.Invoke(edit.Drawing.Id, edit.Current);
	}

	private void FinishDrawing(ChartDrawingKind kind)
	{
		var points = _newPoints.ToList();
		ResetDrawingInput();
		DrawingCreated?.Invoke(kind, points);
	}

	// Cursor in drawing mode: a cross while a tool is armed (on the price chart), a move cursor
	// on an anchor or while moving, a hand on a drawing
	private Cursor? GetDrawingCursor(Point? position)
	{
		if (!IsDrawingMode)
			return null;
		if (_edit is { Moved: true })
			return MoveCursor;
		if (position is not { } p)
			return null;
		if (DrawingTool != null)
			return _newPoints.Count > 0 || _stroke != null || IsOnPriceChart(p)
				? PlaceCursor
				: null;
		if (!CanEditDrawings || _dragStart != null || !CanMapDrawings)
			return null;
		return HitDrawing(p) is { } hit
			? hit.Anchor >= 0
				? MoveCursor
				: HandCursor
			: null;
	}
}
