using LT.App.Stores;
using LT.App.Widgets.OptionChain;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// Typing an underlying into the option chain's header title: it goes through the same path as a
/// contract from the link group (and moves the group along); cancel and blank input change nothing.
/// Disconnected, so no expirations are asked for — the title shows the shown underlying.
/// </summary>
public class OptionChainViewModelTickerTests : IDisposable
{
	private static readonly ContractDto Spy = Stock(756733, "SPY");
	private static readonly ContractDto Msft = Stock(272093, "MSFT");

	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly FakeConnectionManager _connection = new();
	private readonly LinkGroupStore _links = new();
	private readonly ContractManager _contracts;
	private readonly OptionChainViewModel _chain;

	public OptionChainViewModelTickerTests()
	{
		_contracts = new ContractManager(_ibkr, _cache.Settings, _cache.Manager);
		_links.Set(LinkGroup.Red, Spy);
		// Its constructor starts a DispatcherTimer
		_chain = TestUiThread.Invoke(CreateChain);
	}

	[Fact]
	public async Task A_typed_ticker_is_upper_cased_resolved_and_becomes_the_underlying()
	{
		_ibkr.SearchResults = [new ContractDetailsDto { Contract = Msft }];
		_chain.TickerEditor.Begin();

		await _chain.TickerEditor.CommitAsync(" msft");

		Assert.Equal("MSFT", _ibkr.LastSearch!.Symbol);
		Assert.Equal("MSFT", _chain.Title);
		Assert.False(_chain.TickerEditor.IsEditing);
		Assert.Equal(Msft.ConId, _links.Get(LinkGroup.Red)?.ConId);
		Assert.Equal(Msft.ConId, _contracts.GetRecentContracts()[0].ConId);
	}

	[Fact]
	public async Task Blank_input_keeps_the_underlying_and_asks_nothing()
	{
		_chain.TickerEditor.Begin();

		await _chain.TickerEditor.CommitAsync("  ");

		Assert.Equal(0, _ibkr.SearchCalls);
		Assert.Equal("SPY", _chain.Title);
		Assert.False(_chain.TickerEditor.IsEditing);
	}

	[Fact]
	public void Cancel_keeps_the_underlying_and_asks_nothing()
	{
		_chain.TickerEditor.Begin();

		_chain.TickerEditor.Cancel();

		Assert.Equal(0, _ibkr.SearchCalls);
		Assert.Equal("SPY", _chain.Title);
		Assert.False(_chain.TickerEditor.IsEditing);
	}

	[Fact]
	public async Task An_unknown_ticker_shows_an_error_and_keeps_the_underlying()
	{
		_ibkr.SearchResults = [];
		_chain.TickerEditor.Begin();

		await _chain.TickerEditor.CommitAsync("zzzz");

		Assert.Equal("SPY", _chain.Title);
		Assert.False(string.IsNullOrEmpty(_chain.ErrorMessage));
		Assert.Equal(Spy.ConId, _links.Get(LinkGroup.Red)?.ConId);
	}

	[Fact]
	public void A_picked_listing_becomes_the_underlying_like_one_from_the_link_group()
	{
		_chain.TickerEditor.Pick(Msft);

		Assert.Equal("MSFT", _chain.Title);
		Assert.Equal(Msft.ConId, _links.Get(LinkGroup.Red)?.ConId);
	}

	public void Dispose()
	{
		TestUiThread.Invoke(_chain.Dispose);
		GC.SuppressFinalize(this);
	}

	private OptionChainViewModel CreateChain()
	{
		var settings = new SettingsManager(_cache.Settings, _ibkr);
		var marketData = new MarketDataManager(_ibkr, TimeProvider.System);
		return new OptionChainViewModel(
			new OptionChainManager(_ibkr, marketData, _cache.Manager, _cache.Time),
			_contracts,
			marketData,
			_connection,
			new MessageLogManager(),
			new OrderManager(_ibkr, _cache.Manager),
			_links,
			new AccountStore(_connection, settings),
			_cache.Time,
			new DisplayTimeStore(settings, _cache.Time),
			new OrderTicketNavigator()
		);
	}

	private static ContractDto Stock(int conId, string symbol) =>
		new()
		{
			ConId = conId,
			Symbol = symbol,
			SecurityType = "STK",
			Exchange = "SMART",
			Currency = "USD",
		};
}
