using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.MarketDepth;

/// <summary>
/// One change to an order book side: an operation on the row at <see cref="Position"/> (0 = best).
/// <see cref="MarketMaker"/> is the exchange or market maker of the row for SMART / Level 2 depth,
/// null for one exchange's depth. Deletes carry price and size 0.
/// </summary>
public class MarketDepthUpdateDto
{
	public int Position { get; set; }
	public MarketDepthOperation Operation { get; set; }
	public MarketDepthSide Side { get; set; }
	public double Price { get; set; }
	public decimal Size { get; set; }
	public string? MarketMaker { get; set; }
	public bool IsSmartDepth { get; set; }
}
