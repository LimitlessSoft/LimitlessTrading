using LT.Contracts.Constants;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Dtos.Positions;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

/// <summary>Last-known positions, open orders and account summary: per account, always stale.</summary>
public class SnapshotTests
{
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true, OrderClientId = 101 };
	private readonly TestCache _cache = new();

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private static PositionDto Position(string account, int conId, decimal quantity) =>
		new()
		{
			Account = account,
			ContractId = conId,
			Symbol = $"S{conId}",
			Quantity = quantity,
		};

	[Fact]
	public async Task Positions_stream_keeps_each_accounts_open_positions()
	{
		_ibkr.Positions =
		[
			Position("DU1", 1, 10),
			Position("DU2", 2, 5),
			Position("DU1", 3, 7),
			Position("DU1", 1, 0),
		];
		var manager = new PositionManager(_ibkr, _cache.Manager);

		await manager.SubscribePositionsAsync(Ct).ToListAsync(Ct);

		var du1 = manager.GetPositionsSnapshot("DU1");
		var du2 = manager.GetPositionsSnapshot("DU2");
		Assert.True(du1!.IsStale);
		Assert.Equal([3], du1.Value.Select(x => x.ContractId));
		Assert.Equal([2], du2!.Value.Select(x => x.ContractId));
		Assert.Null(manager.GetPositionsSnapshot("DU3"));
	}

	[Fact]
	public async Task Snapshot_keeps_its_fetch_time()
	{
		_ibkr.Positions = [Position("DU1", 1, 10)];
		var manager = new PositionManager(_ibkr, _cache.Manager);
		var at = _cache.Time.GetUtcNow();
		await manager.SubscribePositionsAsync(Ct).ToListAsync(Ct);
		_cache.Time.Advance(TimeSpan.FromDays(2));

		Assert.Equal(at, manager.GetPositionsSnapshot("DU1")!.FetchedAt);
	}

	[Fact]
	public async Task Open_orders_snapshot_is_per_account_and_never_cancellable()
	{
		_ibkr.OpenOrders =
		[
			new OpenOrderDto
			{
				OrderId = 1,
				Account = "DU1",
				ClientId = 101,
				Status = "Submitted",
			},
			new OpenOrderDto { OrderId = 2, Account = "DU2" },
		];
		var manager = new OrderManager(_ibkr, _cache.Manager);

		var live = await manager.GetOpenOrdersAsync("DU1", Ct);
		var snapshot = manager.GetOpenOrdersSnapshot("DU1");

		Assert.True(Assert.Single(live).CanCancel);
		Assert.True(snapshot!.IsStale);
		Assert.False(Assert.Single(snapshot.Value).CanCancel);
		Assert.Null(manager.GetOpenOrdersSnapshot("DU2"));
	}

	[Fact]
	public async Task All_accounts_orders_are_never_kept_as_a_snapshot()
	{
		_ibkr.OpenOrders = [new OpenOrderDto { OrderId = 1, Account = "DU1" }];
		var manager = new OrderManager(_ibkr, _cache.Manager);

		await manager.GetOpenOrdersAsync(null, Ct);

		Assert.Null(manager.GetOpenOrdersSnapshot("DU1"));
		Assert.Empty(_cache.Repository.Entries);
	}

	[Fact]
	public async Task Account_summary_keeps_the_latest_values()
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
		var manager = new AccountManager(_ibkr, _cache.Manager);

		await manager.SubscribeSummaryAsync("DU1", Ct).ToListAsync(Ct);

		var snapshot = manager.GetSummarySnapshot("DU1");
		Assert.True(snapshot!.IsStale);
		Assert.Equal(250m, snapshot.Value.NetLiquidation);
		Assert.Null(manager.GetSummarySnapshot("DU2"));
	}

	[Fact]
	public async Task Disabled_snapshots_are_neither_kept_nor_shown()
	{
		_ibkr.Positions = [Position("DU1", 1, 10)];
		var manager = new PositionManager(_ibkr, _cache.Manager);
		await manager.SubscribePositionsAsync(Ct).ToListAsync(Ct);

		_cache.Settings.Settings.General.CacheSnapshots = false;
		Assert.Null(manager.GetPositionsSnapshot("DU1"));

		_ibkr.Positions = [Position("DU1", 2, 3)];
		await manager.SubscribePositionsAsync(Ct).ToListAsync(Ct);
		_cache.Settings.Settings.General.CacheSnapshots = true;
		Assert.Equal([1], manager.GetPositionsSnapshot("DU1")!.Value.Select(x => x.ContractId));
	}

	[Fact]
	public async Task Clearing_the_cache_drops_snapshots()
	{
		_ibkr.Positions = [Position("DU1", 1, 10)];
		var manager = new PositionManager(_ibkr, _cache.Manager);
		await manager.SubscribePositionsAsync(Ct).ToListAsync(Ct);

		_cache.Manager.Clear();

		Assert.Null(manager.GetPositionsSnapshot("DU1"));
	}

	[Fact]
	public void Snapshot_keys_never_mix_accounts() =>
		Assert.NotEqual(
			LT.Contracts.Helpers.CacheHelpers.GetAccountKey(
				CacheConstants.PositionsSnapshot,
				"DU1"
			),
			LT.Contracts.Helpers.CacheHelpers.GetAccountKey(
				CacheConstants.PositionsSnapshot,
				"DU12"
			)
		);
}
