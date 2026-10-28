using Avalonia.Threading;
using LT.App.Stores;
using LT.App.Widgets.OptionChain;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// The option chain's one-click quantity: a click sends that many contracts (to the fake client,
/// never IB), an empty or out-of-range box sends nothing, and the quantity is saved with the
/// widget. No underlying in the link group, so nothing is loaded; the rows are built by hand.
/// </summary>
public class OptionChainViewModelQuickOrderTests : IDisposable
{
	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly FakeConnectionManager _connection = new();
	private readonly OptionChainViewModel _chain;
	private readonly OptionRowViewModel _row;

	public OptionChainViewModelQuickOrderTests()
	{
		_connection.SetStatus(ConnectionState.Connected, "U1");
		_chain = TestUiThread.Invoke(CreateChain);
		// Runs the chain's posted connect, so it trades
		TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
		_row = new OptionRowViewModel(Strike(), _chain.QuickTradeAsync, () => true);
	}

	[Fact]
	public void Defaults_to_one_contract() => Assert.Equal(1m, _chain.QuickOrderQuantity);

	[Fact]
	public void A_click_sends_the_chosen_quantity_and_the_status_shows_it()
	{
		_chain.QuickOrderQuantity = 5;

		Click(OptionQuoteColumn.Ask);

		var (contract, order) = Assert.Single(_ibkr.PlacedOrders);
		Assert.Equal(11, contract.ConId);
		Assert.Equal(5m, order.Quantity);
		Assert.Equal(OrderAction.Buy, order.Action);
		Assert.Equal("U1", order.Account);
		Assert.Contains("Buy 5 ", _chain.TradeStatus);
		Assert.Null(_chain.ErrorMessage);
	}

	[Theory]
	[InlineData(null)]
	[InlineData(0d)]
	[InlineData(101d)]
	[InlineData(1.5)]
	public void An_empty_or_out_of_range_quantity_sends_nothing(double? quantity)
	{
		_chain.QuickOrderQuantity = (decimal?)quantity;

		Click(OptionQuoteColumn.Bid);

		Assert.Empty(_ibkr.PlacedOrders);
		Assert.Equal(
			"Order not sent: Set a one-click quantity of 1 to 100 whole contracts.",
			_chain.ErrorMessage
		);
	}

	[Fact]
	public void The_quantity_is_saved_and_restored_with_the_widget()
	{
		_chain.QuickOrderQuantity = 12;
		var placement = new WidgetPlacementDto();

		_chain.SaveState(placement);
		_chain.QuickOrderQuantity = 1;
		_chain.LoadState(placement);

		Assert.Equal(12, placement.OptionQuickOrderQuantity);
		Assert.Equal(12m, _chain.QuickOrderQuantity);
	}

	[Theory]
	[InlineData(null)]
	[InlineData(0)]
	[InlineData(500)]
	public void A_missing_or_invalid_saved_quantity_loads_as_the_default(int? saved)
	{
		_chain.QuickOrderQuantity = 9;

		_chain.LoadState(new WidgetPlacementDto { OptionQuickOrderQuantity = saved });

		Assert.Equal(1m, _chain.QuickOrderQuantity);
	}

	[Fact]
	public void An_empty_box_saves_no_quantity()
	{
		_chain.QuickOrderQuantity = null;
		var placement = new WidgetPlacementDto { OptionQuickOrderQuantity = 4 };

		_chain.SaveState(placement);

		Assert.Null(placement.OptionQuickOrderQuantity);
	}

	public void Dispose()
	{
		TestUiThread.Invoke(_chain.Dispose);
		GC.SuppressFinalize(this);
	}

	// On the UI thread, like a real click (the status line starts a DispatcherTimer); the fake
	// client answers synchronously, so the order is sent by the time this returns
	private void Click(OptionQuoteColumn column) =>
		TestUiThread
			.Invoke(() => _chain.QuickTradeAsync(_row, "C", column))
			.GetAwaiter()
			.GetResult();

	private OptionChainViewModel CreateChain()
	{
		var settings = new SettingsManager(_cache.Settings, _ibkr);
		var marketData = new MarketDataManager(_ibkr, TimeProvider.System);
		return new OptionChainViewModel(
			new OptionChainManager(_ibkr, marketData, _cache.Manager),
			new ContractManager(_ibkr, _cache.Settings, _cache.Manager),
			marketData,
			_connection,
			new MessageLogManager(),
			new OrderManager(_ibkr, _cache.Manager),
			new LinkGroupStore(),
			new AccountStore(_connection, settings),
			_cache.Time,
			new DisplayTimeStore(settings, _cache.Time),
			new OrderTicketNavigator()
		);
	}

	private static OptionStrikeDto Strike() =>
		new()
		{
			Strike = 150,
			Call = Option(11, "C"),
			Put = Option(12, "P"),
		};

	private static ContractDto Option(int conId, string right) =>
		new()
		{
			ConId = conId,
			Symbol = "SPY",
			SecurityType = "OPT",
			Exchange = "SMART",
			Currency = "USD",
			LastTradeDateOrContractMonth = "20261120",
			Strike = 150,
			Right = right,
		};
}
