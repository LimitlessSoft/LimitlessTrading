using Avalonia.Threading;
using LT.App.Stores;
using LT.App.Widgets.Chart;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;
using LT.Domain.Managers;
using LT.Indicators.Scripting;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// A chart draws its contract's working orders and moves this app's own limit orders to a new
/// price only after Confirm. Nothing reaches IB: the fake records what would be sent.
/// </summary>
public class ChartViewModelOrderTests : IDisposable
{
	private static readonly ContractDto SpyPut = new()
	{
		ConId = 800000001,
		Symbol = "SPY",
		SecurityType = "OPT",
		Exchange = "SMART",
		Currency = "USD",
		LastTradeDateOrContractMonth = "20261029",
		Strike = 767,
		Right = "P",
	};

	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true, OrderClientId = 101 };
	private readonly FakeConnectionManager _connection = new();
	private readonly OrderManager _orders;
	private readonly OpenOrdersStore _store;
	private readonly List<ChartViewModel> _charts = [];

	public ChartViewModelOrderTests()
	{
		_orders = new OrderManager(_ibkr, _cache.Manager);
		_ibkr.HistoricalBars =
		[
			new BarDto
			{
				Time = _cache.Time.GetUtcNow().AddMinutes(-1),
				Open = 0.80,
				High = 0.90,
				Low = 0.78,
				Close = 0.82,
			},
		];
		_ibkr.OpenOrders = [Order(44, 101), Order(45, 0, limit: 2.10)];
		_connection.SetStatus(ConnectionState.Connected, "U0000001");
		_store = TestUiThread.Invoke(() => new OpenOrdersStore(_orders, _connection, _cache.Time));
	}

	private static OpenOrderDto Order(int orderId, int clientId, double limit = 1.60) =>
		new()
		{
			OrderId = orderId,
			ClientId = clientId,
			PermId = 900000000 + orderId,
			Account = "U0000001",
			Symbol = "SPY 20261029 767 P",
			SecurityType = "OPT",
			Action = "SELL",
			Quantity = 1,
			OrderType = "LMT",
			LimitPrice = limit,
			TimeInForce = "DAY",
			Status = "Submitted",
			Contract = SpyPut,
		};

	[Fact]
	public void The_charted_contracts_orders_are_drawn_and_only_this_apps_can_move()
	{
		var chart = Loaded();

		var own = Line(chart, 44);
		var tws = Line(chart, 45);
		Assert.Equal(1.60, own.Price);
		Assert.True(own.CanMove);
		Assert.Equal(2.10, tws.Price);
		Assert.False(tws.CanMove);
		Assert.Contains("TWS", tws.MoveBlockedReason);
	}

	[Fact]
	public void Dropping_a_line_asks_to_confirm_and_sends_nothing()
	{
		var chart = Loaded();

		TestUiThread.Invoke(() => chart.RequestOrderMove(Line(chart, 44).Id, 1.55));

		Assert.True(chart.IsConfirmingOrderMove);
		Assert.Contains("SELL 1 SPY 20261029 767 P, limit 1.60 → 1.55", chart.OrderMovePrompt);
		Assert.Equal(1.55, Line(chart, 44).Price);
		Assert.True(Line(chart, 44).IsPending);
		Assert.Empty(_ibkr.ModifiedOrders);
	}

	[Fact]
	public void Cancel_puts_the_line_back_and_sends_nothing()
	{
		var chart = Loaded();
		TestUiThread.Invoke(() => chart.RequestOrderMove(Line(chart, 44).Id, 1.55));

		Assert.True(TestUiThread.Invoke(chart.CancelOrderMove));

		Assert.False(chart.IsConfirmingOrderMove);
		Assert.Equal(1.60, Line(chart, 44).Price);
		Assert.False(Line(chart, 44).IsPending);
		Assert.Empty(_ibkr.ModifiedOrders);
		Assert.False(TestUiThread.Invoke(chart.CancelOrderMove));
	}

	[Fact]
	public void Confirm_sends_the_new_price_and_the_line_then_shows_what_ib_holds()
	{
		var chart = Loaded();
		TestUiThread.Invoke(() => chart.RequestOrderMove(Line(chart, 44).Id, 1.55));

		TestUiThread.Invoke(() =>
			chart.ConfirmOrderMoveCommand.ExecuteAsync(null).GetAwaiter().GetResult()
		);

		var (order, price) = Assert.Single(_ibkr.ModifiedOrders);
		Assert.Equal((44, 1.55m), (order.OrderId, price));
		Assert.Empty(_ibkr.PlacedOrders);
		Assert.False(chart.IsConfirmingOrderMove);
		// Until the orders are read again the line stays at the new price
		Assert.Equal(1.55, Line(chart, 44).Price);

		WaitFor(() => Line(chart, 44) is { IsPending: false, Price: 1.55 }, advance: true);
	}

	[Fact]
	public void A_refusal_puts_the_line_back_and_shows_ibs_reason()
	{
		var chart = Loaded();
		_ibkr.ModifyException = new IbkrRequestException(
			110,
			"The price does not conform to the minimum price variation for this contract."
		);
		TestUiThread.Invoke(() => chart.RequestOrderMove(Line(chart, 44).Id, 1.55));

		TestUiThread.Invoke(() =>
			chart.ConfirmOrderMoveCommand.ExecuteAsync(null).GetAwaiter().GetResult()
		);

		Assert.Equal(1.60, Line(chart, 44).Price);
		Assert.False(Line(chart, 44).IsPending);
		Assert.False(chart.IsConfirmingOrderMove);
		Assert.Contains("Order 44 not changed", chart.ErrorMessage);
		Assert.Contains("minimum price variation", chart.ErrorMessage);
	}

	[Fact]
	public void Another_clients_line_never_asks_and_says_why()
	{
		var chart = Loaded();

		TestUiThread.Invoke(() => chart.RequestOrderMove(Line(chart, 45).Id, 2.00));

		Assert.False(chart.IsConfirmingOrderMove);
		Assert.Contains("TWS", chart.ErrorMessage);
		Assert.Equal(2.10, Line(chart, 45).Price);
		Assert.Empty(_ibkr.ModifiedOrders);
	}

	[Fact]
	public void An_order_that_fills_while_confirming_drops_the_question()
	{
		var chart = Loaded();
		TestUiThread.Invoke(() => chart.RequestOrderMove(Line(chart, 44).Id, 1.55));

		_ibkr.OpenOrders = [Order(45, 0, limit: 2.10)];
		_store.Refresh();
		WaitFor(() => !chart.IsConfirmingOrderMove, advance: true);
		TestUiThread.Invoke(() =>
			chart.ConfirmOrderMoveCommand.ExecuteAsync(null).GetAwaiter().GetResult()
		);

		Assert.DoesNotContain(chart.OrderLines, x => x.OrderId == 44);
		Assert.Equal("The order is no longer working.", chart.ErrorMessage);
		Assert.Empty(_ibkr.ModifiedOrders);
	}

	[Fact]
	public void Another_contract_drops_a_question_not_yet_answered()
	{
		var chart = Loaded();
		TestUiThread.Invoke(() => chart.RequestOrderMove(Line(chart, 44).Id, 1.55));

		TestUiThread.Invoke(() =>
			chart.DropContract(
				new ContractDto
				{
					ConId = 756733,
					Symbol = "SPY",
					SecurityType = "STK",
				}
			)
		);

		Assert.False(chart.IsConfirmingOrderMove);
		Assert.Empty(chart.OrderLines);
		Assert.Empty(_ibkr.ModifiedOrders);
	}

	public void Dispose()
	{
		TestUiThread.Invoke(() =>
		{
			foreach (var chart in _charts)
				chart.Dispose();
		});
		GC.SuppressFinalize(this);
	}

	private ChartViewModel Loaded()
	{
		var chart = TestUiThread.Invoke(CreateChart);
		WaitFor(() => chart.OrderLines.Count == 2);
		return chart;
	}

	private static ChartOrderLineDto Line(ChartViewModel chart, int orderId) =>
		chart.OrderLines.Single(x => x.OrderId == orderId && !x.IsStopPrice);

	// Runs the UI thread's posted jobs (moving the fake clock on when asked) until it holds
	private void WaitFor(Func<bool> condition, bool advance = false)
	{
		var until = DateTime.UtcNow.AddSeconds(5);
		while (true)
		{
			TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
			if (TestUiThread.Invoke(condition))
				return;
			Assert.True(DateTime.UtcNow < until, "Timed out");
			if (advance)
				_cache.Time.Advance(TimeSpan.FromMilliseconds(100));
			Thread.Sleep(10);
		}
	}

	private ChartViewModel CreateChart()
	{
		var settings = new SettingsManager(_cache.Settings, _ibkr);
		var chart = new ChartViewModel(
			new ChartManager(
				_ibkr,
				_cache.Manager,
				new MarketDataManager(_ibkr, TimeProvider.System),
				_cache.Time
			),
			new FootprintManager(
				_ibkr,
				_cache.Manager,
				new MarketDataManager(_ibkr, TimeProvider.System),
				new InMemoryFootprintTapeRepository(),
				_cache.Time
			),
			new ContractManager(_ibkr, _cache.Settings, _cache.Manager),
			_connection,
			new MessageLogManager(),
			settings,
			new TradeJournalManager(
				_ibkr,
				new AccountManager(_ibkr, _cache.Manager),
				new InMemoryTradeJournalRepository(),
				_cache.Time
			),
			new PositionManager(_ibkr, _cache.Manager),
			new IndicatorManager(
				new IndicatorEngine(),
				new InMemoryIndicatorScriptRepository(),
				new MessageLogManager()
			),
			new ChartTemplateManager(_cache.Settings),
			new PriceAlertManager(
				new InMemoryPriceAlertRepository(),
				new MarketDataManager(_ibkr, TimeProvider.System),
				new MessageLogManager(),
				_cache.Time
			),
			new ChartDrawingManager(new InMemoryChartDrawingRepository()),
			_orders,
			new ChartDrawingClipboardStore(),
			_store,
			new AccountStore(_connection, settings),
			new LinkGroupStore(),
			new DisplayTimeStore(settings, _cache.Time),
			_cache.Time
		);
		_charts.Add(chart);
		chart.OpenContract(SpyPut);
		return chart;
	}
}
