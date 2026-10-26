namespace LT.Contracts.Dtos.Performance;

/// <summary>A trading day's closed trades and their statistics.</summary>
public class DayPerformanceDto
{
	public DateOnly Day { get; set; }

	/// <summary>Trades closed on the day, oldest first.</summary>
	public IReadOnlyList<RoundTripDto> Trades { get; set; } = [];
	public TradeStatsDto Stats { get; set; } = new();

	/// <summary>Trades still open at the end of the day (not in the statistics).</summary>
	public int OpenTrades { get; set; }

	/// <summary>Fills recorded on the day.</summary>
	public int FillCount { get; set; }
}
