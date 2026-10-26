namespace LT.Contracts.Dtos.MarketDepth;

/// <summary>A snapshot of an order book: each side best first, at most the requested rows.</summary>
public class OrderBookDto
{
	public IReadOnlyList<OrderBookLevelDto> Bids { get; set; } = [];
	public IReadOnlyList<OrderBookLevelDto> Asks { get; set; } = [];

	/// <summary>Rows were aggregated over exchanges (SMART depth).</summary>
	public bool IsSmartDepth { get; set; }
}
