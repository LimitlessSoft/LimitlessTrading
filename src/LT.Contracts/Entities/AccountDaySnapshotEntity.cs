using LT.Contracts.Enums;

namespace LT.Contracts.Entities;

/// <summary>
/// One account's values on one trading day, as last seen by the app that day. Written by the
/// account performance recorder; IB's socket API has no history of its own.
/// </summary>
public class AccountDaySnapshotEntity
{
	/// <summary>Trading day: the date in New York (see AccountPerformanceConstants).</summary>
	public DateOnly Date { get; set; }
	public string Currency { get; set; } = string.Empty;
	public decimal? NetLiquidation { get; set; }
	public double? DailyPnl { get; set; }
	public double? RealizedPnl { get; set; }
	public double? UnrealizedPnl { get; set; }

	/// <summary>
	/// Net deposits (+) and withdrawals (-) of the day in the base currency, from Flex; already left
	/// out of <see cref="DailyPnl"/>. Null when unknown (recorded days).
	/// </summary>
	public double? CashFlow { get; set; }

	/// <summary>Recorded live, or imported from Flex (then net liquidation is the end-of-day NAV).</summary>
	public AccountDaySource Source { get; set; }
	public DateTimeOffset FirstUpdate { get; set; }
	public DateTimeOffset LastUpdate { get; set; }
}
