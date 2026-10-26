using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using LT.Contracts.Dtos.AccountPerformance;
using LT.Contracts.Helpers;
using static LT.App.Widgets.AccountPerformance.ChartDrawing;

namespace LT.App.Widgets.AccountPerformance;

/// <summary>
/// Net liquidation per recorded day, placed by date (days the app didn't run leave a gap in the
/// spacing, not a fake point). Hover shows the day under the cursor. Cash movements, when given,
/// are badges along the bottom on the day they reach net liquidation.
/// </summary>
public class EquityCurveChart : Control
{
	public static readonly StyledProperty<IReadOnlyList<AccountDaySnapshotDto>?> DaysProperty =
		AvaloniaProperty.Register<EquityCurveChart, IReadOnlyList<AccountDaySnapshotDto>?>(
			nameof(Days)
		);

	/// <summary>Deposits, withdrawals and transfers to mark with badges; null or empty: none.</summary>
	public static readonly StyledProperty<IReadOnlyList<CashMovementDto>?> CashMovementsProperty =
		AvaloniaProperty.Register<EquityCurveChart, IReadOnlyList<CashMovementDto>?>(
			nameof(CashMovements)
		);

	private Point? _pointer;

	static EquityCurveChart()
	{
		AffectsRender<EquityCurveChart>(DaysProperty, CashMovementsProperty);
		ClipToBoundsProperty.OverrideDefaultValue<EquityCurveChart>(true);
	}

	public IReadOnlyList<AccountDaySnapshotDto>? Days
	{
		get => GetValue(DaysProperty);
		set => SetValue(DaysProperty, value);
	}

	public IReadOnlyList<CashMovementDto>? CashMovements
	{
		get => GetValue(CashMovementsProperty);
		set => SetValue(CashMovementsProperty, value);
	}

	public override void Render(DrawingContext context)
	{
		var size = Bounds.Size;
		context.DrawRectangle(Background, null, new Rect(size));

		var points = (Days ?? [])
			.Where(x => x.NetLiquidation != null)
			.Select(x => (x.Date, Value: (double)x.NetLiquidation!.Value))
			.ToList();
		if (points.Count == 0 || size.Width < ValueAxisWidth + 40 || size.Height < 60)
		{
			var empty = Text("No net liquidation recorded yet", 12, TextBrush);
			context.DrawText(
				empty,
				new Point((size.Width - empty.Width) / 2, (size.Height - empty.Height) / 2)
			);
			return;
		}

		var plot = new Rect(
			8,
			TopPadding,
			size.Width - ValueAxisWidth - 8,
			size.Height - TopPadding - DateAxisHeight
		);
		var low = points.Min(x => x.Value);
		var high = points.Max(x => x.Value);
		if (high - low < Math.Max(1, Math.Abs(high) * 0.001))
		{
			var half = Math.Max(1, Math.Abs(high) * 0.005);
			high += half;
			low -= half;
		}
		var padding = (high - low) * 0.08;
		low -= padding;
		high += padding;

		var first = points[0].Date.DayNumber;
		var span = Math.Max(1, points[^1].Date.DayNumber - first);
		double X(DateOnly date) =>
			points.Count == 1
				? plot.Center.X
				: plot.X + (date.DayNumber - first) / (double)span * plot.Width;
		double Y(double value) => plot.Bottom - (value - low) / (high - low) * plot.Height;

		DrawValueGrid(context, plot, low, high, Y);

		// date labels: first, last and evenly between, at least 70 px apart
		var labelCount = Math.Clamp((int)(plot.Width / 70), 1, points.Count);
		var lastLabelRight = double.MinValue;
		for (var i = 0; i < labelCount; i++)
		{
			var point = points[
				labelCount == 1 ? points.Count - 1 : i * (points.Count - 1) / (labelCount - 1)
			];
			var label = Text(ChartDrawing.Date(point.Date), 11, TextBrush);
			var x = Math.Clamp(X(point.Date) - label.Width / 2, plot.X, plot.Right - label.Width);
			if (x < lastLabelRight + 8)
				continue;
			context.DrawText(label, new Point(x, plot.Bottom + 3));
			lastLabelRight = x + label.Width;
		}

		// area + line
		if (points.Count > 1)
		{
			var area = new StreamGeometry();
			using (var ctx = area.Open())
			{
				ctx.BeginFigure(new Point(X(points[0].Date), plot.Bottom), true);
				foreach (var point in points)
					ctx.LineTo(new Point(X(point.Date), Y(point.Value)));
				ctx.LineTo(new Point(X(points[^1].Date), plot.Bottom));
				ctx.EndFigure(true);
			}
			context.DrawGeometry(AreaBrush, null, area);

			var line = new StreamGeometry();
			using (var ctx = line.Open())
			{
				ctx.BeginFigure(new Point(X(points[0].Date), Y(points[0].Value)), false);
				foreach (var point in points.Skip(1))
					ctx.LineTo(new Point(X(point.Date), Y(point.Value)));
				ctx.EndFigure(false);
			}
			context.DrawGeometry(null, LinePen, line);
		}
		if (points.Count <= 60)
			foreach (var point in points)
				context.DrawEllipse(
					LineBrush,
					null,
					new Point(X(point.Date), Y(point.Value)),
					2.5,
					2.5
				);

		// deposits / withdrawals / transfers, on the day net liquidation steps
		var movements = AccountPerformanceHelpers.AssignCashMovements(
			points.Select(x => x.Date).ToList(),
			CashMovements ?? []
		);
		DrawCashMovementBadges(context, plot, movements, i => X(points[i].Date));

		// hover: the nearest day
		var hoveredIndex = points.Count - 1;
		if (_pointer is { } p && plot.Contains(p))
		{
			hoveredIndex = Enumerable
				.Range(0, points.Count)
				.MinBy(i => Math.Abs(X(points[i].Date) - p.X));
			var x = Math.Round(X(points[hoveredIndex].Date)) + 0.5;
			context.DrawLine(CrosshairPen, new Point(x, plot.Y), new Point(x, plot.Bottom));
			context.DrawEllipse(
				LabelBrush,
				null,
				new Point(x, Y(points[hoveredIndex].Value)),
				3.5,
				3.5
			);
		}
		var hovered = points[hoveredIndex];
		var readout = Text(
			$"{hovered.Date.ToString("ddd dd MMM yyyy", CultureInfo.CurrentCulture)}   Net liq {hovered.Value.ToString("#,0.00", CultureInfo.CurrentCulture)}",
			12,
			LabelBrush
		);
		context.DrawText(readout, new Point(8, 3));
		DrawCashMovementReadout(context, readout, movements.GetValueOrDefault(hoveredIndex));
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
}
