using System.ComponentModel;
using LT.App.Stores;
using LT.App.Widgets.Watchlist;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// Pinning watchlist rows: pinned rows stay on top in pin order whatever column is sorted, the rest
/// follow the sort. Disconnected, so no quotes are asked for; quotes are pushed into the rows.
/// </summary>
public class WatchlistViewModelPinTests : IDisposable
{
	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly WatchlistManager _watchlists;
	private WatchlistViewModel _watchlist = null!;

	public WatchlistViewModelPinTests()
	{
		_watchlists = new WatchlistManager(
			_cache.Settings,
			new ContractManager(_ibkr, _cache.Settings, _cache.Manager)
		);
		// Saved order, with the last prices pushed below
		foreach (var (conId, symbol) in new[] { (1, "SPY"), (2, "AAPL"), (3, "MSFT"), (4, "NVDA") })
			_watchlists.Add(
				_watchlists.GetActive().Id,
				new ContractDto
				{
					ConId = conId,
					Symbol = symbol,
					SecurityType = "STK",
					Exchange = "SMART",
					Currency = "USD",
				}
			);
		Create();
	}

	[Fact]
	public void Pinned_rows_go_on_top_in_pin_order_and_are_saved()
	{
		Pin("NVDA");
		Pin("AAPL");

		Assert.Equal(["NVDA", "AAPL", "SPY", "MSFT"], Symbols());
		Assert.True(Row("NVDA").IsPinned);
		Assert.Equal(
			["NVDA", "AAPL"],
			_watchlists.GetPinned(_watchlists.GetActive().Id).Select(x => x.Symbol)
		);
	}

	[Fact]
	public void A_sort_in_either_direction_leaves_the_pinned_rows_alone()
	{
		Quote(("SPY", 500), ("AAPL", 200), ("MSFT", 400), ("NVDA", 900));
		Pin("NVDA");
		Pin("AAPL");

		TestUiThread.Invoke(() => _watchlist.SortBy(WatchlistSortColumn.Last));
		Assert.Equal(ListSortDirection.Ascending, _watchlist.SortDirection);
		Assert.Equal(["NVDA", "AAPL", "MSFT", "SPY"], Symbols());

		TestUiThread.Invoke(() => _watchlist.SortBy(WatchlistSortColumn.Last));
		Assert.Equal(ListSortDirection.Descending, _watchlist.SortDirection);
		Assert.Equal(["NVDA", "AAPL", "SPY", "MSFT"], Symbols());
	}

	[Fact]
	public void An_unpinned_row_rejoins_the_sort()
	{
		Quote(("SPY", 500), ("AAPL", 200), ("MSFT", 400), ("NVDA", 900));
		TestUiThread.Invoke(() => _watchlist.SortBy(WatchlistSortColumn.Last));
		Pin("NVDA");
		Assert.Equal(["NVDA", "AAPL", "MSFT", "SPY"], Symbols());

		Select("NVDA");
		Assert.True(_watchlist.CanUnpin);
		TestUiThread.Invoke(() => _watchlist.UnpinCommand.Execute(null));

		Assert.Equal(["AAPL", "MSFT", "SPY", "NVDA"], Symbols());
		Assert.Empty(_watchlists.GetPinned(_watchlists.GetActive().Id));
		Assert.True(_watchlist.CanPin);
	}

	[Fact]
	public void A_reopened_watchlist_shows_its_pins_on_top()
	{
		Pin("MSFT");
		Pin("SPY");
		TestUiThread.Invoke(_watchlist.Dispose);

		Create();

		Assert.Equal(["MSFT", "SPY", "AAPL", "NVDA"], Symbols());
		Assert.Equal([0, 1], _watchlist.Rows.Take(2).Select(x => x.PinIndex!.Value));
	}

	[Fact]
	public void Removing_a_pinned_row_closes_the_gap()
	{
		Pin("MSFT");
		Pin("SPY");
		Pin("NVDA");

		TestUiThread.Invoke(() => _watchlist.RemoveCommand.Execute(Row("SPY")));

		Assert.Equal(["MSFT", "NVDA", "AAPL"], Symbols());
		Assert.Equal([0, 1], _watchlist.Rows.Take(2).Select(x => x.PinIndex!.Value));
	}

	public void Dispose()
	{
		TestUiThread.Invoke(_watchlist.Dispose);
		GC.SuppressFinalize(this);
	}

	// Its constructor starts DispatcherTimers
	private void Create() =>
		_watchlist = TestUiThread.Invoke(() =>
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
				new LinkGroupStore()
			);
		});

	private string[] Symbols() => _watchlist.Rows.Select(x => x.Symbol).ToArray();

	private WatchlistRowViewModel Row(string symbol) =>
		_watchlist.Rows.Single(x => x.Symbol == symbol);

	private void Select(string symbol) =>
		TestUiThread.Invoke(() => _watchlist.SelectedRow = Row(symbol));

	// Right-click selects the row; the menu pins the selection
	private void Pin(string symbol)
	{
		Select(symbol);
		Assert.True(_watchlist.CanPin);
		TestUiThread.Invoke(() => _watchlist.PinCommand.Execute(null));
	}

	private void Quote(params (string Symbol, double Last)[] quotes) =>
		TestUiThread.Invoke(() =>
		{
			foreach (var (symbol, last) in quotes)
			{
				var row = Row(symbol);
				row.Pending.Set(new QuoteDto { Last = last, Close = last });
				row.Flush();
			}
		});
}
