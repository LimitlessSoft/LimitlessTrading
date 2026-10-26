namespace LT.Contracts.Dtos.AccountPerformance;

/// <summary>One cell of the daily P&amp;L month grid.</summary>
public class CalendarDayDto
{
	public DateOnly Date { get; set; }

	/// <summary>False for the leading / trailing days of the neighbouring months.</summary>
	public bool IsInMonth { get; set; }
	public bool IsToday { get; set; }

	/// <summary>The day's recorded daily P&amp;L; null when nothing was recorded.</summary>
	public double? Pnl { get; set; }

	/// <summary>The day's deposits, withdrawals and transfers (empty when none or not asked for).</summary>
	public IReadOnlyList<CashMovementDto> CashMovements { get; set; } = [];
}
