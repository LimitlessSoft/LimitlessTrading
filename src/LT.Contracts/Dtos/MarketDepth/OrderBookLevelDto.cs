namespace LT.Contracts.Dtos.MarketDepth;

/// <summary>One row of an order book side; <see cref="MarketMaker"/> as in the IB update.</summary>
public class OrderBookLevelDto
{
	public double Price { get; set; }
	public decimal Size { get; set; }
	public string? MarketMaker { get; set; }
}
