using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using LT.App.Helpers;
using LT.Contracts.Dtos.AccountPerformance;
using LT.Contracts.Helpers;
using static LT.App.Widgets.AccountPerformance.ChartDrawing;

namespace LT.App.Widgets.AccountPerformance;

/// <summary>
/// One bar per recorded day's daily P&amp;L, green above zero and red below. Days are spaced
/// evenly (only recorded days are drawn). Hover shows the day under the cursor. Cash movements,
/// when given, are badges along the bottom under the bar of their day (or the next one).
/// </summary>
public class DailyPnlBarChart : Control
{
	public static readonly StyledProperty<IReadOnlyList<AccountDaySnapshotDto>?> DaysProperty =
		AvaloniaProperty.Register<DailyPnlBarChart, IReadOnlyList<AccountDaySnapshotDto>?>(
			nameof(Days)
		);

	private const double MaxBarWidth = 28;

	/// <summary>Deposits, withdrawals and transfers to mark with badges; null or empty: none.</summary>
	public static readonly StyledProperty<IReadOnlyList<CashMovementDto>?> CashMovementsProperty =
		AvaloniaProperty.Register<DailyPnlBarChart, IReadOnlyList<CashMovementDto>?>(
			nameof(CashMovements)
		);

	private Point? _pointer;

	static DailyPnlBarChart()
	{
		AffectsRender<DailyPnlBarChart>(DaysProperty, CashMovementsProperty);
		ClipToBoundsProperty.OverrideDefaultValue<DailyPnlBarChart>(true);
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

		var bars = (Days ?? [])
			.Where(x => x.DailyPnl != null)
			.Select(x => (x.Date, Value: x.DailyPnl!.Value))
			.ToList();
		if (bars.Count == 0 || size.Width < ValueAxisWidth + 40 || size.Height < 60)
		{
			var empty = Text("No daily P&L recorded yet", 12, TextBrush);
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
		var high = Math.Max(0, bars.Max(x => x.Value));
		var low = Math.Min(0, bars.Min(x => x.Value));
		if (high - low < 1)
		{
			high += 1;
			low -= 1;
		}
		var padding = (high - low) * 0.08;
		high += padding;
		low -= padding;

		var slot = plot.Width / bars.Count;
		var barWidth = Math.Max(1, Math.Min(MaxBarWidth, slot * 0.7));
		double X(int index) => plot.X + (index + 0.5) * slot;
		double Y(double value) => plot.Bottom - (value - low) / (high - low) * plot.Height;

		DrawValueGrid(context, plot, low, high, Y);
		var zeroY = Math.Round(Y(0)) + 0.5;
		context.DrawLine(ZeroPen, new Point(plot.X, zeroY), new Point(plot.Right, zeroY));

		var labelEvery = Math.Max(1, (int)Math.Ceiling(60 / slot));
		for (var i = 0; i < bars.Count; i++)
		{
			var (date, value) = bars[i];
			var top = Y(Math.Max(0, value));
			var bottom = Y(Math.Min(0, value));
			context.DrawRectangle(
				value >= 0 ? UpBrush : DownBrush,
				null,
				new Rect(X(i) - barWidth / 2, top, barWidth, Math.Max(1, bottom - top))
			);
			if ((bars.Count - 1 - i) % labelEvery != 0)
				continue;
			var label = Text(ChartDrawing.Date(date), 11, TextBrush);
			var x = Math.Clamp(X(i) - label.Width / 2, plot.X, plot.Right - label.Width);
			context.DrawText(label, new Point(x, plot.Bottom + 3));
		}

		var movements = AccountPerformanceHelpers.AssignCashMovements(
			bars.Select(x => x.Date).ToList(),
			CashMovements ?? []
		);
		DrawCashMovementBadges(context, plot, movements, X);

		var hovered = bars.Count - 1;
		if (_pointer is { } p && plot.Contains(p))
		{
			hovered = Math.Clamp((int)((p.X - plot.X) / slot), 0, bars.Count - 1);
			var x = Math.Round(X(hovered)) + 0.5;
			context.DrawLine(CrosshairPen, new Point(x, plot.Y), new Point(x, plot.Bottom));
		}
		var (hoveredDate, hoveredValue) = bars[hovered];
		var readout = Text(
			$"{hoveredDate.ToString("ddd dd MMM yyyy", CultureInfo.CurrentCulture)}   Daily P&L {Sensitive.Apply(hoveredValue.ToString("+#,0.00;-#,0.00;0.00", CultureInfo.CurrentCulture))}",
			12,
			hoveredValue >= 0 ? UpBrush : DownBrush
		);
		context.DrawText(readout, new Point(8, 3));
		DrawCashMovementReadout(context, readout, movements.GetValueOrDefault(hovered));
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
