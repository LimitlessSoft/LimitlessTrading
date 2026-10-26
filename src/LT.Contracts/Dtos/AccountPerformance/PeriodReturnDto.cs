namespace LT.Contracts.Dtos.AccountPerformance;

/// <summary>Return over a span of days: summed daily P&amp;L and the chained daily returns.</summary>
public class PeriodReturnDto
{
	/// <summary>Sum of the days' daily P&amp;L; null when no day in the span has one.</summary>
	public double? Pnl { get; set; }

	/// <summary>Time-weighted return in percent (2.5 = +2.5 %); null when no day has one.</summary>
	public double? Percent { get; set; }
}
