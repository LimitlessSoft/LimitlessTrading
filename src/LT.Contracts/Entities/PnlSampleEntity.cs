namespace LT.Contracts.Entities;

/// <summary>One point of an account's intraday P&amp;L curve (IB's daily P&amp;L at that time).</summary>
public class PnlSampleEntity
{
	public string Account { get; set; } = string.Empty;
	public DateTimeOffset Time { get; set; }
	public double DailyPnl { get; set; }
}
