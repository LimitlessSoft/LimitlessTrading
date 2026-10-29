using Avalonia.Threading;
using LT.App.Stores;
using LT.App.Widgets.OptionChain;
using LT.App.Widgets.OrderTicket;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// The option chain's strike cell opens an order ticket popup for its call or put (never sends:
/// orders go to the fake client only and none is expected), the ticket reviews that option as a
/// market or limit order, and the chain's hidden quote columns are saved with the widget.
/// </summary>
public class OptionChainViewModelOrderTicketTests : IDisposable
{
	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly FakeConnectionManager _connection = new();
	private readonly OrderTicketNavigator _navigator = new();
	private readonly List<OrderDraftDto> _opened = [];
	private readonly OptionChainViewModel _chain;

	public OptionChainViewModelOrderTicketTests()
	{
		_connection.SetStatus(ConnectionState.Connected, "U1");
		_navigator.OpenRequested += _opened.Add;
		_chain = TestUiThread.Invoke(CreateChain);
		// Runs the chain's posted connect
		TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
	}

	[Fact]
	public void The_call_half_opens_a_ticket_for_the_call_and_sends_nothing()
	{
		_chain.QuickOrderQuantity = 3;
		var row = Row(Strike());

		TestUiThread.Invoke(() => row.OpenCallTicketCommand.Execute(null));

		var draft = Assert.Single(_opened);
		Assert.Equal(11, draft.Contract.ConId);
		Assert.Equal(OrderAction.Buy, draft.Action);
		Assert.Equal(3m, draft.Quantity);
		Assert.Equal("U1", draft.Account);
		Assert.Empty(_ibkr.PlacedOrders);
	}

	[Fact]
	public void The_put_half_opens_a_ticket_for_the_put()
	{
		var row = Row(Strike());

		TestUiThread.Invoke(() => row.OpenPutTicketCommand.Execute(null));

		Assert.Equal(12, Assert.Single(_opened).Contract.ConId);
		Assert.Empty(_ibkr.PlacedOrders);
	}

	[Fact]
	public void A_side_without_an_option_or_a_disconnected_chain_opens_nothing()
	{
		var noPut = Row(Strike(withPut: false));
		var offline = new OptionRowViewModel(
			Strike(),
			_chain.QuickTradeAsync,
			() => false,
			_chain.OpenOrderTicket
		);

		Assert.False(noPut.OpenPutTicketCommand.CanExecute(null));
		Assert.True(noPut.OpenCallTicketCommand.CanExecute(null));
		Assert.False(offline.OpenCallTicketCommand.CanExecute(null));
		Assert.False(offline.OpenPutTicketCommand.CanExecute(null));
	}

	[Fact]
	public void The_ticket_reviews_the_option_as_a_limit_order_without_sending()
	{
		var ticket = TestUiThread.Invoke(() =>
		{
			var settings = new SettingsManager(_cache.Settings, _ibkr);
			var vm = new OrderTicketViewModel(
				new OrderManager(_ibkr, _cache.Manager),
				new MarketDataManager(_ibkr, TimeProvider.System),
				_connection,
				new MessageLogManager(),
				new AccountStore(_connection, settings),
				new LinkGroupStore()
			);
			vm.Prefill(
				new OrderDraftDto
				{
					Contract = Option(11, "C"),
					Account = "U1",
					Action = OrderAction.Buy,
					Quantity = 2,
					LimitPrice = 1.23m,
				}
			);
			return vm;
		});

		Assert.Equal("1.23", ticket.LimitPriceText);
		Assert.False(ticket.IsLimit);

		TestUiThread.Invoke(() =>
		{
			ticket.SelectedOrderType = ticket.OrderTypes.Single(x => x.Value == OrderType.Limit);
			ticket.ReviewCommand.Execute(null);
		});

		Assert.Null(ticket.ErrorMessage);
		Assert.True(ticket.IsReviewing);
		Assert.Equal(
			"Buy 2 SPY 20261120 150 C (SMART) limit 1.23, DAY, account U1",
			ticket.ReviewText
		);
		Assert.Empty(_ibkr.PlacedOrders);
		TestUiThread.Invoke(ticket.Dispose);
	}

	[Fact]
	public void Every_quote_column_is_shown_at_first_and_nothing_is_saved()
	{
		var placement = new WidgetPlacementDto();

		_chain.SaveState(placement);

		Assert.All(Enum.GetValues<OptionQuoteColumn>(), x => Assert.True(_chain.IsColumnShown(x)));
		Assert.Null(placement.OptionHiddenColumns);
	}

	[Fact]
	public void Hidden_columns_are_saved_and_restored_with_the_widget()
	{
		var changes = 0;
		_chain.ColumnVisibilityChanged += () => changes++;

		_chain.SetColumnShown(OptionQuoteColumn.Volume, false);
		_chain.SetColumnShown(OptionQuoteColumn.Last, false);
		_chain.SetColumnShown(OptionQuoteColumn.Last, false);
		var placement = new WidgetPlacementDto();
		_chain.SaveState(placement);
		_chain.SetColumnShown(OptionQuoteColumn.Volume, true);
		_chain.LoadState(placement);

		Assert.Equal(
			[OptionQuoteColumn.Volume, OptionQuoteColumn.Last],
			placement.OptionHiddenColumns
		);
		Assert.False(_chain.IsColumnShown(OptionQuoteColumn.Volume));
		Assert.False(_chain.IsColumnShown(OptionQuoteColumn.Last));
		Assert.True(_chain.IsColumnShown(OptionQuoteColumn.Bid));
		// Two hides, a repeat that changes nothing, a show, the load
		Assert.Equal(4, changes);
	}

	public void Dispose()
	{
		TestUiThread.Invoke(_chain.Dispose);
		GC.SuppressFinalize(this);
	}

	private OptionRowViewModel Row(OptionStrikeDto strike) =>
		new(strike, _chain.QuickTradeAsync, () => true, _chain.OpenOrderTicket);

	private OptionChainViewModel CreateChain()
	{
		var settings = new SettingsManager(_cache.Settings, _ibkr);
		var marketData = new MarketDataManager(_ibkr, TimeProvider.System);
		return new OptionChainViewModel(
			new OptionChainManager(_ibkr, marketData, _cache.Manager, _cache.Time),
			new ContractManager(_ibkr, _cache.Settings, _cache.Manager),
			marketData,
			_connection,
			new MessageLogManager(),
			new OrderManager(_ibkr, _cache.Manager),
			new LinkGroupStore(),
			new AccountStore(_connection, settings),
			_cache.Time,
			new DisplayTimeStore(settings, _cache.Time),
			_navigator
		);
	}

	private static OptionStrikeDto Strike(bool withPut = true) =>
		new()
		{
			Strike = 150,
			Call = Option(11, "C"),
			Put = withPut ? Option(12, "P") : null,
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
