using Avalonia.Threading;
using LT.App.Stores;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.WidgetsCore;

/// <summary>
/// One reading of the working orders for every chart: only while watched and connected, again
/// soon after this app's orders changed, cleared when nobody watches or the connection drops.
/// </summary>
public class OpenOrdersStoreTests
{
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true, OrderClientId = 101 };
	private readonly FakeConnectionManager _connection = new();
	private readonly TestCache _cache = new();
	private readonly OrderManager _manager;
	private readonly OpenOrdersStore _store;
	private int _changed;

	public OpenOrdersStoreTests()
	{
		_manager = new OrderManager(_ibkr, _cache.Manager);
		_ibkr.OpenOrders = [Order(44, 1.60)];
		_connection.SetStatus(ConnectionState.Connected, "U0000001");
		_store = TestUiThread.Invoke(() => new OpenOrdersStore(_manager, _connection, _cache.Time));
		_store.Changed += () => _changed++;
	}

	private static OpenOrderDto Order(int orderId, double limit) =>
		new()
		{
			OrderId = orderId,
			ClientId = 101,
			PermId = 900000000 + orderId,
			Account = "U0000001",
			Action = "SELL",
			Quantity = 1,
			OrderType = "LMT",
			LimitPrice = limit,
			Status = "Submitted",
			Contract = new ContractDto { ConId = 800000001 },
		};

	[Fact]
	public void Nobody_watching_nothing_is_read()
	{
		RunJobs();

		Assert.Equal(0, _ibkr.OpenOrdersRequests);
		Assert.Empty(_store.Orders);
	}

	[Fact]
	public void Watched_the_orders_are_read_once_for_every_watcher()
	{
		TestUiThread.Invoke(() =>
		{
			_store.Watch();
			_store.Watch();
		});

		WaitFor(() => _store.Orders.Count == 1);

		Assert.Equal(1, _ibkr.OpenOrdersRequests);
		Assert.Equal(1.60, _store.Orders[0].LimitPrice);
		Assert.True(_changed > 0);
	}

	[Fact]
	public async Task After_this_apps_orders_change_they_are_read_again_before_the_interval()
	{
		TestUiThread.Invoke(() => _store.Watch());
		WaitFor(() => _store.Orders.Count == 1);
		_ibkr.OpenOrders = [Order(44, 1.55)];

		await _manager.CancelOrderAsync(45, TestContext.Current.CancellationToken);
		var advanced = WaitFor(() => _store.Orders[0].LimitPrice == 1.55, advance: true);

		Assert.True(advanced < AccountConstants.OrdersRefreshInterval, $"read after {advanced}");
	}

	[Fact]
	public void Disconnected_the_orders_are_cleared_and_no_longer_read()
	{
		TestUiThread.Invoke(() => _store.Watch());
		WaitFor(() => _store.Orders.Count == 1);

		_connection.SetStatus(ConnectionState.Disconnected);
		WaitFor(() => _store.Orders.Count == 0);
		var requests = _ibkr.OpenOrdersRequests;
		_cache.Time.Advance(AccountConstants.OrdersRefreshInterval * 2);
		RunJobs();

		Assert.Equal(requests, _ibkr.OpenOrdersRequests);
	}

	[Fact]
	public void When_the_last_watcher_leaves_the_orders_are_cleared()
	{
		var first = TestUiThread.Invoke(() => _store.Watch());
		var second = TestUiThread.Invoke(() => _store.Watch());
		WaitFor(() => _store.Orders.Count == 1);

		TestUiThread.Invoke(first.Dispose);
		RunJobs();
		Assert.Single(_store.Orders);

		TestUiThread.Invoke(second.Dispose);
		Assert.Empty(_store.Orders);
	}

	private static void RunJobs() => TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());

	// Runs the UI thread's posted jobs (moving the fake clock on when asked) until the condition
	// holds; returns how far the clock moved
	private TimeSpan WaitFor(Func<bool> condition, bool advance = false)
	{
		var advanced = TimeSpan.Zero;
		var until = DateTime.UtcNow.AddSeconds(5);
		while (true)
		{
			RunJobs();
			if (TestUiThread.Invoke(condition))
				return advanced;
			Assert.True(DateTime.UtcNow < until, "Timed out");
			if (advance)
			{
				_cache.Time.Advance(TimeSpan.FromMilliseconds(50));
				advanced += TimeSpan.FromMilliseconds(50);
			}
			Thread.Sleep(10);
		}
	}
}
