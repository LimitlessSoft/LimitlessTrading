namespace LT.Contracts.Dtos.MarketData;

/// <summary>Level 1 snapshot folded from ticks (live or delayed tick types).</summary>
public class QuoteDto
{
	public double? Bid { get; set; }
	public double? Ask { get; set; }
	public double? Last { get; set; }
	public decimal? BidSize { get; set; }
	public decimal? AskSize { get; set; }
	public decimal? LastSize { get; set; }
	public decimal? Volume { get; set; }

	/// <summary>The 90-day average daily volume in shares; only with generic tick 165.</summary>
	public decimal? AverageVolume { get; set; }
	public double? High { get; set; }
	public double? Low { get; set; }
	public double? Open { get; set; }
	public double? Close { get; set; }
	public DateTimeOffset? LastTime { get; set; }
	public bool IsHalted { get; set; }
	public bool IsDelayed { get; set; }

	/// <summary>
	/// The underlying's 30-day option implied volatility as a fraction (0.25 = 25%); only with
	/// generic tick 106.
	/// </summary>
	public double? ImpliedVolatility { get; set; }

	public QuoteDto Clone() => (QuoteDto)MemberwiseClone();
}
