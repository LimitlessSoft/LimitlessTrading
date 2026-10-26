using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.Orders;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class AccountManagerTests
{
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly TestCache _cache = new();

	[Fact]
	public async Task Summary_follows_the_latest_value_of_each_tag()
	{
		_ibkr.AccountValues =
		[
			new AccountValueDto
			{
				Account = "DU1",
				Tag = "NetLiquidation",
				Value = "100",
				Currency = "USD",
			},
			new AccountValueDto
			{
				Account = "DU1",
				Tag = "NetLiquidation",
				Value = "250",
				Currency = "USD",
			},
		];

		var summaries = await new AccountManager(_ibkr, _cache.Manager)
			.SubscribeSummaryAsync("DU1", TestContext.Current.CancellationToken)
			.ToListAsync(TestContext.Current.CancellationToken);

		Assert.Equal([100m, 250m], summaries.Select(x => x.NetLiquidation));
	}

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

		var summary = await new AccountManager(_ibkr, _cache.Manager)
			.SubscribeSummaryAsync("DU1", TestContext.Current.CancellationToken)
			.LastAsync(TestContext.Current.CancellationToken);

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

		var orders = await new OrderManager(_ibkr, _cache.Manager).GetOpenOrdersAsync(
			"DU1",
			TestContext.Current.CancellationToken
		);

		Assert.Equal([2], orders.Select(x => x.OrderId));
	}

	[Fact]
	public async Task Only_orders_of_the_order_connection_can_be_cancelled()
	{
		_ibkr.OrderClientId = 101;
		_ibkr.OpenOrders =
		[
			new OpenOrderDto
			{
				OrderId = 1,
				ClientId = 101,
				Status = "Submitted",
			},
			new OpenOrderDto
			{
				OrderId = 2,
				ClientId = 0,
				Status = "Submitted",
			},
			new OpenOrderDto
			{
				OrderId = 3,
				ClientId = 101,
				Status = "PendingCancel",
			},
		];

		var orders = await new OrderManager(_ibkr, _cache.Manager).GetOpenOrdersAsync(
			null,
			TestContext.Current.CancellationToken
		);

		Assert.Equal([true, false, false], orders.Select(x => x.CanCancel));
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

		var executions = await new ExecutionManager(
			_ibkr,
			_cache.Manager,
			_cache.Time
		).GetExecutionsAsync(null, TestContext.Current.CancellationToken);

		Assert.Equal(["b", "a"], executions.Value.Select(x => x.ExecId));
	}
}
