namespace LT.Contracts.Dtos.Charts;

/// <summary>
/// The trades of one chart bar by price (<c>FootprintHelpers.AddTrade</c>), keyed by
/// <see cref="FootprintLevelDto.TickIndex"/>. Built and read on the UI thread only.
/// </summary>
public class FootprintBarDto
{
	/// <summary>The chart bar's start.</summary>
	public DateTimeOffset Time { get; set; }

	/// <summary>Price step of <see cref="FootprintLevelDto.TickIndex"/> (the contract's tick).</summary>
	public double TickSize { get; set; }

	public Dictionary<long, FootprintLevelDto> Levels { get; set; } = [];
	public decimal BidVolume { get; set; }
	public decimal AskVolume { get; set; }

	/// <summary>Ask minus bid volume: positive when buyers were more aggressive.</summary>
	public decimal Delta => AskVolume - BidVolume;
}
