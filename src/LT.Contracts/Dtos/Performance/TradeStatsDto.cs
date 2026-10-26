namespace LT.Contracts.Dtos.Performance;

/// <summary>Statistics of closed trades, net of commission. Nulls where they are undefined.</summary>
public class TradeStatsDto
{
	public int TradeCount { get; set; }
	public int Wins { get; set; }
	public int Losses { get; set; }
	public double NetPnl { get; set; }
	public double GrossPnl { get; set; }
	public double Commissions { get; set; }

	/// <summary>Share of trades with a positive net P&amp;L, 0 to 1.</summary>
	public double? WinRate { get; set; }
	public double? AverageWin { get; set; }

	/// <summary>Negative: the average net P&amp;L of losing trades.</summary>
	public double? AverageLoss { get; set; }

	/// <summary>Sum of wins over the absolute sum of losses; null without losses.</summary>
	public double? ProfitFactor { get; set; }
	public double? LargestWin { get; set; }
	public double? LargestLoss { get; set; }

	/// <summary>Average net P&amp;L per trade.</summary>
	public double? Expectancy { get; set; }
}
