using System.Globalization;
using LT.Contracts.Dtos.AccountPerformance;
using LT.Contracts.Enums;

namespace LT.App.Widgets.AccountPerformance;

/// <summary>One cell of the daily P&amp;L calendar.</summary>
public class CalendarDayViewModel(CalendarDayDto day, string currency = "")
{
	public string DayText { get; } = day.Date.Day.ToString(CultureInfo.CurrentCulture);
	public string PnlText { get; } =
		day.IsInMonth && day.Pnl is { } pnl ? Compact(pnl) : string.Empty;
	public string ToolTipText { get; } = BuildToolTip(day, currency);
	public bool IsInMonth { get; } = day.IsInMonth;
	public bool IsToday { get; } = day.IsToday;
	public bool IsUp { get; } = day.IsInMonth && day.Pnl > 0;
	public bool IsDown { get; } = day.IsInMonth && day.Pnl < 0;

	/// <summary>One badge per kind of the day's deposits, withdrawals and transfers.</summary>
	public IReadOnlyList<CashMovementKind> CashMovementKinds { get; } =
		day.CashMovements.Select(x => x.Kind).Distinct().ToList();
	public bool HasCashMovements => CashMovementKinds.Count > 0;

	private static string BuildToolTip(CalendarDayDto day, string currency)
	{
		var date = day.Date.ToString("ddd dd MMM yyyy", CultureInfo.CurrentCulture);
		var text = day.Pnl is { } value
			? $"{date}: {value.ToString("+#,0.00;-#,0.00;0.00", CultureInfo.CurrentCulture)}"
			: date;
		return day.CashMovements.Count == 0
			? text
			: string.Join(
				Environment.NewLine,
				[text, .. day.CashMovements.Select(x => CashMovementText.Full(x, currency))]
			);
	}

	/// <summary>Fits a cell: +1,234 or -12.3K, no cents.</summary>
	private static string Compact(double pnl) =>
		Math.Abs(pnl) >= 100_000
			? (pnl / 1_000).ToString("+#,0.#K;-#,0.#K;0", CultureInfo.CurrentCulture)
			: pnl.ToString("+#,0;-#,0;0", CultureInfo.CurrentCulture);
}
