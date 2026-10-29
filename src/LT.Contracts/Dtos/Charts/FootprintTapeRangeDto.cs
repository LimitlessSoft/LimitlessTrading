namespace LT.Contracts.Dtos.Charts;

/// <summary>
/// A stretch of time the footprint trade cache holds every trade of: all trades IB has with a
/// time from <see cref="From"/> to before <see cref="To"/>, oldest first (in IB's order within a
/// second).
/// </summary>
public class FootprintTapeRangeDto
{
	public DateTimeOffset From { get; set; }
	public DateTimeOffset To { get; set; }
	public IReadOnlyList<FootprintTapeTradeDto> Trades { get; set; } = [];
}
