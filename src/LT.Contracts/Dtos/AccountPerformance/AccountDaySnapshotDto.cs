namespace LT.Contracts.Dtos.AccountPerformance;

/// <summary>One recorded trading day of an account; today's row follows the live values.</summary>
public class AccountDaySnapshotDto
{
	public DateOnly Date { get; set; }
	public string Currency { get; set; } = string.Empty;
	public decimal? NetLiquidation { get; set; }
	public double? DailyPnl { get; set; }
	public double? RealizedPnl { get; set; }
	public double? UnrealizedPnl { get; set; }
	public DateTimeOffset FirstUpdate { get; set; }
	public DateTimeOffset LastUpdate { get; set; }
}
