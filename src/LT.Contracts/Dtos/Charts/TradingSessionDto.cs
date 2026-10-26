namespace LT.Contracts.Dtos.Charts;

/// <summary>One session of a trading day, in the exchange's wall-clock time.</summary>
public class TradingSessionDto
{
	public TimeOnly Start { get; set; }

	/// <summary>May run past midnight (a session listed under the day it starts).</summary>
	public TimeSpan Length { get; set; }
}
