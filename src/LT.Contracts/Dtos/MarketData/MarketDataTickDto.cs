namespace LT.Contracts.Dtos.MarketData;

/// <summary>One raw tick; <see cref="Field"/> is the IB tick type id.</summary>
public class MarketDataTickDto
{
	public int Field { get; set; }
	public double? Price { get; set; }
	public decimal? Size { get; set; }
	public double? Value { get; set; }
	public string? StringValue { get; set; }
}
