using LT.App.Stores;
using LT.App.Widgets.Watchlist;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Requests.Watchlists;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// Several watchlist widgets at once: each shows its own watchlist (saved per instance), changes
/// go to the list the widget shows, and widgets showing the same list follow each other.
/// Disconnected, so no quotes are asked for.
/// </summary>
public class WatchlistViewModelInstanceTests : IDisposable
{
	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly WatchlistManager _watchlists;
	private readonly List<WatchlistViewModel> _widgets = [];
	private readonly string _mainId;

	public WatchlistViewModelInstanceTests()
	{
		_watchlists = new WatchlistManager(
			_cache.Settings,
			new ContractManager(_ibkr, _cache.Settings, _cache.Manager)
		);
		_mainId = _watchlists.GetActive().Id;
		_watchlists.Add(_mainId, Stock(1, "SPY"));
	}

	[Fact]
	public void A_symbol_added_in_one_widget_goes_to_the_list_that_widget_shows()
	{
		var first = Create();
		var secondId = _watchlists.SaveName(new SaveWatchlistNameRequest { Name = "Second" }).Id;
		var second = Create();

		TestUiThread.Invoke(() => first.AddContract(Stock(2, "AAPL")));
		TestUiThread.Invoke(() => second.AddContract(Stock(3, "MSFT")));

		Assert.Equal(["SPY", "AAPL"], _watchlists.GetItems(_mainId).Select(x => x.Symbol));
		Assert.Equal(["MSFT"], _watchlists.GetItems(secondId).Select(x => x.Symbol));
		Assert.Equal(["SPY", "AAPL"], Symbols(first));
		Assert.Equal(["MSFT"], Symbols(second));
	}

	[Fact]
	public void Picking_a_list_in_one_widget_leaves_the_other_on_its_own()
	{
		_watchlists.SaveName(new SaveWatchlistNameRequest { Name = "Second" });
		_watchlists.SetActive(_mainId);
		var first = Create();
		var second = Create();

		var item = second.Watchlists.Single(x => x.Name == "Second");
		TestUiThread.Invoke(() => second.SelectWatchlistCommand.Execute(item));

		Assert.Equal(["SPY"], Symbols(first));
		Assert.Empty(second.Rows);
		Assert.Equal(_mainId, first.ActiveWatchlist!.Id);
	}

	[Fact]
	public void Widgets_showing_the_same_list_follow_each_other()
	{
		var first = Create();
		var second = Create();

		TestUiThread.Invoke(() => first.AddContract(Stock(2, "AAPL")));
		Assert.Equal(["SPY", "AAPL"], Symbols(second));

		TestUiThread.Invoke(() => second.RemoveCommand.Execute(second.Rows[0]));
		Assert.Equal(["AAPL"], Symbols(first));
	}

	[Fact]
	public void A_widget_whose_list_is_deleted_elsewhere_shows_the_active_one()
	{
		var secondId = _watchlists.SaveName(new SaveWatchlistNameRequest { Name = "Second" }).Id;
		var onSecond = Create();
		var other = Create();

		var item = other.Watchlists.Single(x => x.Id == secondId);
		TestUiThread.Invoke(() => other.ConfirmDeleteCommand.Execute(item));

		Assert.Equal(_mainId, onSecond.ActiveWatchlist!.Id);
		Assert.Equal(["SPY"], Symbols(onSecond));
		Assert.Single(onSecond.Watchlists);
	}

	[Fact]
	public void A_restored_widget_shows_the_list_it_was_saved_with()
	{
		var secondId = _watchlists.SaveName(new SaveWatchlistNameRequest { Name = "Second" }).Id;
		_watchlists.Add(secondId, Stock(3, "MSFT"));
		var saved = Create();
		var placement = new WidgetPlacementDto();
		saved.SaveState(placement);
		_watchlists.SetActive(_mainId);

		var restored = Create();
		TestUiThread.Invoke(() => restored.LoadState(placement));

		Assert.Equal(secondId, placement.WatchlistId);
		Assert.Equal(["MSFT"], Symbols(restored));
	}

	[Fact]
	public void A_widget_saved_with_a_deleted_list_opens_on_the_active_one()
	{
		var widget = Create();

		TestUiThread.Invoke(() =>
			widget.LoadState(new WidgetPlacementDto { WatchlistId = "gone" })
		);

		Assert.Equal(_mainId, widget.ActiveWatchlist!.Id);
		Assert.Equal(["SPY"], Symbols(widget));
	}

	[Fact]
	public void A_closed_widget_stops_following_the_lists()
	{
		var first = Create();
		var closed = Create();
		TestUiThread.Invoke(closed.Dispose);
		_widgets.Remove(closed);

		TestUiThread.Invoke(() => first.AddContract(Stock(2, "AAPL")));

		Assert.Equal(["SPY"], Symbols(closed));
	}

	public void Dispose()
	{
		foreach (var widget in _widgets)
			TestUiThread.Invoke(widget.Dispose);
		GC.SuppressFinalize(this);
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

	private static string[] Symbols(WatchlistViewModel widget) =>
		widget.Rows.Select(x => x.Symbol).ToArray();

	// Its constructor starts DispatcherTimers
	private WatchlistViewModel Create()
	{
		var widget = TestUiThread.Invoke(() =>
		{
			var log = new MessageLogManager();
			return new WatchlistViewModel(
				_watchlists,
				new ContractManager(_ibkr, _cache.Settings, _cache.Manager),
				new MarketDataManager(_ibkr, TimeProvider.System),
				new FakeConnectionManager(),
				log,
				new SettingsManager(_cache.Settings, _ibkr),
				new SharesOutstandingManager(
					new FakeSecApiClient(),
					new InMemorySharesOutstandingRepository(),
					log,
					_cache.Time
				),
				new LinkGroupStore(),
				TimeProvider.System
			);
		});
		_widgets.Add(widget);
		return widget;
	}
}
