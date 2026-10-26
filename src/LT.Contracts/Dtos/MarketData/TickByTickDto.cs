using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.MarketData;

/// <summary>
/// One IB tick-by-tick tick. <see cref="Time"/> comes from IB in whole seconds (many ticks share
/// one). Trades (<see cref="TickByTickType.Last"/> / <see cref="TickByTickType.AllLast"/>) carry
/// <see cref="Price"/> and <see cref="Size"/>; <see cref="TickByTickType.MidPoint"/> the midpoint
/// in <see cref="Price"/>; <see cref="TickByTickType.BidAsk"/> the bid / ask fields.
/// </summary>
public class TickByTickDto
{
	public TickByTickType Type { get; set; }
	public DateTimeOffset Time { get; set; }
	public double Price { get; set; }
	public decimal Size { get; set; }
	public double BidPrice { get; set; }
	public double AskPrice { get; set; }
	public decimal BidSize { get; set; }
	public decimal AskSize { get; set; }

	/// <summary>An odd lot (AllLast only), not reported to the tape.</summary>
	public bool IsUnreported { get; set; }
}
