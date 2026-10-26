using System.Globalization;
using LT.Contracts.Dtos.AccountPerformance;

namespace LT.App.Widgets.AccountPerformance;

/// <summary>One cell of the daily P&amp;L calendar.</summary>
public class CalendarDayViewModel(CalendarDayDto day)
{
	public string DayText { get; } = day.Date.Day.ToString(CultureInfo.CurrentCulture);
	public string PnlText { get; } =
		day.IsInMonth && day.Pnl is { } pnl ? Compact(pnl) : string.Empty;
	public string ToolTipText { get; } =
		day.Pnl is { } value
			? $"{day.Date.ToString("ddd dd MMM yyyy", CultureInfo.CurrentCulture)}: {value.ToString("+#,0.00;-#,0.00;0.00", CultureInfo.CurrentCulture)}"
			: day.Date.ToString("ddd dd MMM yyyy", CultureInfo.CurrentCulture);
	public bool IsInMonth { get; } = day.IsInMonth;
	public bool IsToday { get; } = day.IsToday;
	public bool IsUp { get; } = day.IsInMonth && day.Pnl > 0;
	public bool IsDown { get; } = day.IsInMonth && day.Pnl < 0;

	/// <summary>Fits a cell: +1,234 or -12.3K, no cents.</summary>
	private static string Compact(double pnl) =>
		Math.Abs(pnl) >= 100_000
			? (pnl / 1_000).ToString("+#,0.#K;-#,0.#K;0", CultureInfo.CurrentCulture)
			: pnl.ToString("+#,0;-#,0;0", CultureInfo.CurrentCulture);
}
