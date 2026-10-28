namespace LT.Contracts.Dtos.Charts;

/// <summary>
/// Volume traded at one price of a footprint bar: <see cref="TickIndex"/> is the price in ticks
/// (price / tick size, rounded), so prices never compare as doubles.
/// </summary>
public class FootprintLevelDto
{
	public long TickIndex { get; set; }

	/// <summary>Volume of trades at the bid (sellers hitting it).</summary>
	public decimal BidVolume { get; set; }

	/// <summary>Volume of trades at the ask (buyers lifting it).</summary>
	public decimal AskVolume { get; set; }
}
