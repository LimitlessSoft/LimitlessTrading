using LT.Contracts.Dtos.Performance;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class TradeStatsHelpersTests
{
	[Fact]
	public void Statistics_are_net_of_commission()
	{
		var stats = TradeStatsHelpers.Compute([
			Trade(101, 1),
			Trade(51, 1),
			Trade(-29, 1),
			Trade(-9, 1),
			Trade(1, 1),
		]);

		Assert.Equal(5, stats.TradeCount);
		Assert.Equal(2, stats.Wins);
		Assert.Equal(2, stats.Losses); // the 0 net trade is neither
		Assert.Equal(115, stats.GrossPnl, 6);
		Assert.Equal(5, stats.Commissions, 6);
		Assert.Equal(110, stats.NetPnl, 6);
		Assert.Equal(0.4, stats.WinRate!.Value, 6);
		Assert.Equal(75, stats.AverageWin!.Value, 6);
		Assert.Equal(-20, stats.AverageLoss!.Value, 6);
		Assert.Equal(150.0 / 40, stats.ProfitFactor!.Value, 6);
		Assert.Equal(100, stats.LargestWin!.Value, 6);
		Assert.Equal(-30, stats.LargestLoss!.Value, 6);
		Assert.Equal(22, stats.Expectancy!.Value, 6);
	}

	[Fact]
	public void Without_losses_there_is_no_profit_factor()
	{
		var stats = TradeStatsHelpers.Compute([Trade(10, 0)]);

		Assert.Null(stats.ProfitFactor);
		Assert.Null(stats.AverageLoss);
		Assert.Equal(1, stats.WinRate);
	}

	[Fact]
	public void No_trades_leaves_ratios_undefined()
	{
		var stats = TradeStatsHelpers.Compute([]);

		Assert.Equal(0, stats.TradeCount);
		Assert.Null(stats.WinRate);
		Assert.Null(stats.Expectancy);
	}

	[Fact]
	public void Open_trades_are_not_counted()
	{
		var open = Trade(500, 0);
		open.IsClosed = false;

		Assert.Equal(0, TradeStatsHelpers.Compute([open]).TradeCount);
	}

	private static RoundTripDto Trade(double gross, double commission) =>
		new()
		{
			IsClosed = true,
			GrossPnl = gross,
			Commission = commission,
		};
}
