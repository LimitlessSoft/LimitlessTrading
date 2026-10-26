using LT.Contracts.Dtos.Performance;

namespace LT.Contracts.Helpers;

public static class TradeStatsHelpers
{
	/// <summary>
	/// Statistics of <paramref name="trades"/> (closed ones; open trades are skipped), net of
	/// commission. A trade that nets exactly 0 counts as a trade but neither a win nor a loss.
	/// </summary>
	public static TradeStatsDto Compute(IEnumerable<RoundTripDto> trades)
	{
		var closed = trades.Where(x => x.IsClosed).ToList();
		var wins = closed.Where(x => x.NetPnl > 0).Select(x => x.NetPnl).ToList();
		var losses = closed.Where(x => x.NetPnl < 0).Select(x => x.NetPnl).ToList();
		var net = closed.Sum(x => x.NetPnl);
		return new TradeStatsDto
		{
			TradeCount = closed.Count,
			Wins = wins.Count,
			Losses = losses.Count,
			NetPnl = net,
			GrossPnl = closed.Sum(x => x.GrossPnl),
			Commissions = closed.Sum(x => x.Commission),
			WinRate = closed.Count > 0 ? (double)wins.Count / closed.Count : null,
			AverageWin = wins.Count > 0 ? wins.Average() : null,
			AverageLoss = losses.Count > 0 ? losses.Average() : null,
			ProfitFactor = losses.Count > 0 ? wins.Sum() / -losses.Sum() : null,
			LargestWin = wins.Count > 0 ? wins.Max() : null,
			LargestLoss = losses.Count > 0 ? losses.Min() : null,
			Expectancy = closed.Count > 0 ? net / closed.Count : null,
		};
	}
}
