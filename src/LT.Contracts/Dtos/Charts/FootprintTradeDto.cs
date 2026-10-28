using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Charts;

/// <summary>One trade a footprint counts, with the side that started it.</summary>
public class FootprintTradeDto
{
	/// <summary>IB's time, whole seconds.</summary>
	public DateTimeOffset Time { get; set; }
	public double Price { get; set; }
	public decimal Size { get; set; }
	public FootprintSide Side { get; set; }

	/// <summary>
	/// Told by the tick rule (the price against the trade before it) because no bid / ask was
	/// known; otherwise by the trade price against the latest bid / ask.
	/// </summary>
	public bool ByTickRule { get; set; }
}
