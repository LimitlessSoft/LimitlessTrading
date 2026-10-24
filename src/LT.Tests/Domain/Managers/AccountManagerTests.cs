using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.Orders;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class AccountManagerTests
{
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };

	[Fact]
	public async Task Summary_is_built_for_the_requested_account()
	{
		_ibkr.AccountValues =
		[
			new AccountValueDto
			{
				Account = "DU1",
				Tag = "NetLiquidation",
				Value = "100000.50",
				Currency = "USD",
			},
			new AccountValueDto
			{
				Account = "DU2",
				Tag = "NetLiquidation",
				Value = "5",
				Currency = "EUR",
			},
			new AccountValueDto
			{
				Account = "DU1",
				Tag = "BuyingPower",
				Value = "400000",
				Currency = "USD",
			},
		];

		var summary = await new AccountManager(_ibkr).GetSummaryAsync(
			"DU1",
			TestContext.Current.CancellationToken
		);

		Assert.Equal(
			(100000.50m, 400000m, "USD"),
			(summary.NetLiquidation, summary.BuyingPower, summary.Currency)
		);
		Assert.Null(summary.TotalCashValue);
	}

	[Fact]
	public async Task Open_orders_are_filtered_by_account()
	{
		_ibkr.OpenOrders =
		[
			new OpenOrderDto { OrderId = 2, Account = "DU1" },
			new OpenOrderDto { OrderId = 1, Account = "DU2" },
		];

		var orders = await new OrderManager(_ibkr).GetOpenOrdersAsync(
			"DU1",
			TestContext.Current.CancellationToken
		);

		Assert.Equal([2], orders.Select(x => x.OrderId));
	}

	[Fact]
	public async Task Executions_are_newest_first()
	{
		var now = DateTimeOffset.Now;
		_ibkr.Executions =
		[
			new ExecutionDto { ExecId = "a", Time = now.AddMinutes(-5) },
			new ExecutionDto { ExecId = "b", Time = now },
		];

		var executions = await new ExecutionManager(_ibkr).GetExecutionsAsync(
			null,
			TestContext.Current.CancellationToken
		);

		Assert.Equal(["b", "a"], executions.Select(x => x.ExecId));
	}
}
