using System.ComponentModel;
using LT.App.Stores;
using LT.App.Widgets.Watchlist;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// Managing the watchlist widget's columns: show or hide each one (never the last shown), sort by
/// any of them, hidden ones included, live as quotes tick, and both saved with the widget.
/// Disconnected: quotes are written to the rows as the market data task would.
/// </summary>
public class WatchlistViewModelColumnsTests : IDisposable
{
	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly WatchlistManager _watchlists;
	private readonly List<WatchlistViewModel> _widgets = [];
	private readonly WatchlistViewModel _watchlist;

	public WatchlistViewModelColumnsTests()
	{
		_watchlists = new WatchlistManager(
			_cache.Settings,
			new ContractManager(_ibkr, _cache.Settings, _cache.Manager)
		);
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
		_watchlist = Create();
	}

	[Fact]
	public void Every_column_is_listed_and_shown_at_first()
	{
		Assert.Equal(
			["Symbol", "Last", "Chg %", "Bid / Ask", "Vol", "Avg Vol", "Mkt Cap"],
			_watchlist.Columns.Select(x => x.Name)
		);
		Assert.All(_watchlist.Columns, x => Assert.True(x.IsShown));
		Assert.All(_watchlist.Columns, x => Assert.True(x.CanToggle));
		Assert.False(_watchlist.HasSort);
	}

	[Fact]
	public void Average_volume_is_shown_compact_sorts_the_rows_and_outlives_quotes_without_it()
	{
		TestUiThread.Invoke(() =>
		{
			foreach (
				var (symbol, average) in new[]
				{
					("SPY", 75_000_000m),
					("AAPL", 43_502_835m),
					("MSFT", 21_000_000m),
				}
			)
			{
				var row = Row(symbol);
				row.Pending.Set(new QuoteDto { Last = 1, AverageVolume = average });
				row.Flush();
			}
			// A snapshot carries no generic ticks: the average stays
			Row("AAPL").Pending.Set(new QuoteDto { Last = 2 });
			Row("AAPL").Flush();
			_watchlist.SortBy(
				WatchlistSortColumn.AverageVolume,
				null,
				ListSortDirection.Descending
			);
		});

		Assert.Equal("43.5M", Row("AAPL").AverageVolume);
		Assert.Equal(43_502_835m, Row("AAPL").AverageVolumeValue);
		Assert.Equal("—", Row("NVDA").AverageVolume);
		// Unknown goes last
		Assert.Equal(["SPY", "AAPL", "MSFT", "NVDA"], Symbols());
	}

	[Fact]
	public void A_hidden_column_still_sorts_the_rows_and_a_line_says_so()
	{
		Quote(("SPY", 500), ("AAPL", 200), ("MSFT", 400), ("NVDA", 900));
		var changes = 0;
		_watchlist.ColumnVisibilityChanged += () => changes++;

		TestUiThread.Invoke(() => Option(WatchlistSortColumn.Last).IsShown = false);
		Assert.False(_watchlist.IsColumnShown(WatchlistSortColumn.Last, null));
		Assert.Equal(1, changes);
		Assert.False(_watchlist.IsSortHidden);

		TestUiThread.Invoke(() =>
			_watchlist.SortByColumnCommand.Execute(Option(WatchlistSortColumn.Last))
		);
		Assert.Equal(["AAPL", "MSFT", "SPY", "NVDA"], Symbols());
		Assert.True(Option(WatchlistSortColumn.Last).IsSorted);
		Assert.True(_watchlist.IsSortHidden);
		Assert.Equal("Sorted by Last (hidden) ↑", _watchlist.HiddenSortText);

		// The arrow again flips it, like a header click
		TestUiThread.Invoke(() =>
			_watchlist.SortByColumnCommand.Execute(Option(WatchlistSortColumn.Last))
		);
		Assert.Equal(["NVDA", "SPY", "MSFT", "AAPL"], Symbols());
		Assert.Equal("Sorted by Last (hidden) ↓", _watchlist.HiddenSortText);
		Assert.Equal("↓", Option(WatchlistSortColumn.Last).SortGlyph);

		// Shown again: its header has the arrow, the line goes
		TestUiThread.Invoke(() => Option(WatchlistSortColumn.Last).IsShown = true);
		Assert.False(_watchlist.IsSortHidden);
		Assert.Null(_watchlist.HiddenSortText);
		Assert.Equal(["NVDA", "SPY", "MSFT", "AAPL"], Symbols());
	}

	[Fact]
	public void Rows_sorted_by_a_hidden_quote_column_follow_the_ticks()
	{
		Quote(("SPY", 500), ("AAPL", 200), ("MSFT", 400), ("NVDA", 900));
		TestUiThread.Invoke(() =>
		{
			_watchlist.SetColumnShown(WatchlistSortColumn.Last, null, false);
			_watchlist.SortBy(WatchlistSortColumn.Last, null, ListSortDirection.Descending);
		});
		Assert.Equal(["NVDA", "SPY", "MSFT", "AAPL"], Symbols());

		// The market data task writes the newest quote; the UI flush and resort ticks apply it
		TestUiThread.Invoke(() =>
		{
			Row("AAPL").Pending.Set(new QuoteDto { Last = 1000, Close = 1000 });
			_watchlist.FlushQuotes();
		});
		Assert.Equal(["NVDA", "SPY", "MSFT", "AAPL"], Symbols());
		TestUiThread.Invoke(_watchlist.ResortIfStale);

		Assert.Equal(["AAPL", "NVDA", "SPY", "MSFT"], Symbols());
	}

	[Fact]
	public void A_hidden_custom_column_still_sorts_by_its_values()
	{
		var column = AddColumn("Target");
		Type(column, "SPY", "3");
		Type(column, "AAPL", "1");
		Type(column, "NVDA", "2");
		var option = Assert.Single(_watchlist.Columns, x => x.CustomColumnId == column.Id);
		Assert.True(option.IsCustom);

		TestUiThread.Invoke(() =>
		{
			option.IsShown = false;
			_watchlist.SortByColumnCommand.Execute(option);
		});

		Assert.False(_watchlist.IsColumnShown(WatchlistSortColumn.Custom, column.Id));
		Assert.Equal(["AAPL", "NVDA", "SPY", "MSFT"], Symbols());
		Assert.Equal("Sorted by Target (hidden) ↑", _watchlist.HiddenSortText);

		// A new value re-sorts at once, hidden or not
		Type(column, "MSFT", "0");
		Assert.Equal(["MSFT", "AAPL", "NVDA", "SPY"], Symbols());
	}

	[Fact]
	public void The_last_shown_column_cannot_be_hidden()
	{
		TestUiThread.Invoke(() =>
		{
			foreach (var option in _watchlist.Columns.Skip(1))
				option.IsShown = false;
		});
		var symbol = Option(WatchlistSortColumn.Symbol);
		Assert.False(symbol.CanToggle);
		Assert.True(Option(WatchlistSortColumn.Last).CanToggle);

		TestUiThread.Invoke(() => symbol.IsShown = false);

		Assert.True(symbol.IsShown);
		Assert.True(_watchlist.IsColumnShown(WatchlistSortColumn.Symbol, null));
		Assert.False(_watchlist.CanToggleColumn(WatchlistSortColumn.Symbol, null));
	}

	[Fact]
	public void Deleting_the_only_shown_custom_column_brings_symbol_back()
	{
		var column = AddColumn("Target");
		TestUiThread.Invoke(() =>
		{
			foreach (var option in _watchlist.Columns.Where(x => !x.IsCustom))
				option.IsShown = false;
		});
		Assert.False(_watchlist.IsColumnShown(WatchlistSortColumn.Symbol, null));

		TestUiThread.Invoke(() => _watchlist.ConfirmDeleteColumnCommand.Execute(column));

		Assert.True(_watchlist.IsColumnShown(WatchlistSortColumn.Symbol, null));
		Assert.Equal(["Symbol"], _watchlist.Columns.Where(x => x.IsShown).Select(x => x.Name));
	}

	[Fact]
	public void Clearing_the_sort_goes_back_to_the_saved_order()
	{
		Quote(("SPY", 500), ("AAPL", 200), ("MSFT", 400), ("NVDA", 900));
		TestUiThread.Invoke(() =>
			_watchlist.SortBy(WatchlistSortColumn.Last, null, ListSortDirection.Ascending)
		);
		Assert.True(_watchlist.HasSort);

		TestUiThread.Invoke(() => _watchlist.ClearSortCommand.Execute(null));

		Assert.Equal(WatchlistSortColumn.None, _watchlist.SortColumn);
		Assert.Equal(["SPY", "AAPL", "MSFT", "NVDA"], Symbols());
		Assert.All(_watchlist.Columns, x => Assert.False(x.IsSorted));
	}

	[Fact]
	public void Hidden_columns_and_the_sort_are_saved_with_the_widget()
	{
		var column = AddColumn("Target");
		Type(column, "SPY", "3");
		Type(column, "AAPL", "1");
		TestUiThread.Invoke(() =>
		{
			_watchlist.SetColumnShown(WatchlistSortColumn.Volume, null, false);
			_watchlist.SetColumnShown(WatchlistSortColumn.MarketCap, null, false);
			_watchlist.SetColumnShown(WatchlistSortColumn.Custom, column.Id, false);
			_watchlist.SortBy(WatchlistSortColumn.Custom, column.Id, ListSortDirection.Descending);
		});
		var placement = new WidgetPlacementDto();
		_watchlist.SaveState(placement);

		Assert.Equal(
			[WatchlistSortColumn.Volume, WatchlistSortColumn.MarketCap],
			placement.WatchlistHiddenColumns
		);
		Assert.Equal([column.Id], placement.WatchlistHiddenCustomColumns);
		Assert.Equal(WatchlistSortColumn.Custom, placement.WatchlistSortBy);
		Assert.Equal(column.Id, placement.WatchlistSortCustomColumnId);
		Assert.True(placement.WatchlistSortDescending);

		var restored = Create();
		TestUiThread.Invoke(() => restored.LoadState(placement));

		Assert.False(restored.IsColumnShown(WatchlistSortColumn.Volume, null));
		Assert.False(restored.IsColumnShown(WatchlistSortColumn.MarketCap, null));
		Assert.False(restored.IsColumnShown(WatchlistSortColumn.Custom, column.Id));
		Assert.True(restored.IsColumnShown(WatchlistSortColumn.Last, null));
		Assert.Equal(WatchlistSortColumn.Custom, restored.SortColumn);
		Assert.Equal(ListSortDirection.Descending, restored.SortDirection);
		Assert.Equal(["SPY", "AAPL", "MSFT", "NVDA"], restored.Rows.Select(x => x.Symbol));
		Assert.True(restored.IsSortHidden);
		Assert.False(Assert.Single(restored.Columns, x => x.CustomColumnId == column.Id).IsShown);
	}

	[Fact]
	public void A_widget_with_nothing_hidden_or_sorted_saves_nothing()
	{
		var placement = new WidgetPlacementDto();

		_watchlist.SaveState(placement);

		Assert.Null(placement.WatchlistHiddenColumns);
		Assert.Null(placement.WatchlistHiddenCustomColumns);
		Assert.Null(placement.WatchlistSortBy);
		Assert.Null(placement.WatchlistSortCustomColumnId);
		Assert.Null(placement.WatchlistSortDescending);
	}

	[Fact]
	public void A_saved_sort_by_a_column_that_is_gone_falls_back_to_the_saved_order()
	{
		TestUiThread.Invoke(() =>
			_watchlist.LoadState(
				new WidgetPlacementDto
				{
					WatchlistSortBy = WatchlistSortColumn.Custom,
					WatchlistSortCustomColumnId = "gone",
					WatchlistSortDescending = true,
					WatchlistHiddenCustomColumns = ["gone"],
				}
			)
		);

		Assert.Equal(WatchlistSortColumn.None, _watchlist.SortColumn);
		Assert.Equal(ListSortDirection.Ascending, _watchlist.SortDirection);
		Assert.False(_watchlist.IsSortHidden);
		Assert.Equal(["SPY", "AAPL", "MSFT", "NVDA"], Symbols());
	}

	[Fact]
	public void Hidden_columns_and_the_sort_round_trip_with_the_layout()
	{
		var settings = new SettingsManager(_cache.Settings, _ibkr);
		var layout = settings.GetLayout();
		layout.Widgets =
		[
			new WidgetPlacementDto
			{
				Id = "watchlist",
				Tool = WorkspaceTool.Watchlist,
				WatchlistHiddenColumns = [WatchlistSortColumn.Bid],
				WatchlistHiddenCustomColumns = ["c1"],
				WatchlistSortBy = WatchlistSortColumn.Bid,
				WatchlistSortDescending = true,
			},
		];

		settings.SaveLayout(layout);

		var saved = Assert.Single(settings.GetLayout().Widgets);
		Assert.Equal([WatchlistSortColumn.Bid], saved.WatchlistHiddenColumns);
		Assert.Equal(["c1"], saved.WatchlistHiddenCustomColumns);
		Assert.Equal(WatchlistSortColumn.Bid, saved.WatchlistSortBy);
		Assert.Null(saved.WatchlistSortCustomColumnId);
		Assert.True(saved.WatchlistSortDescending);
	}

	public void Dispose()
	{
		foreach (var widget in _widgets)
			TestUiThread.Invoke(widget.Dispose);
		GC.SuppressFinalize(this);
	}

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

	private WatchlistColumnOptionViewModel Option(WatchlistSortColumn column) =>
		_watchlist.Columns.Single(x => x.Column == column);

	// "+ Add column", then the typed name and Enter
	private WatchlistColumnItemViewModel AddColumn(string name) =>
		TestUiThread.Invoke(() =>
		{
			_watchlist.AddColumnCommand.Execute(null);
			var item = _watchlist.CustomColumns[^1];
			item.RenameText = name;
			_watchlist.CommitRenameColumnCommand.Execute(item);
			return _watchlist.CustomColumns.Single(x => x.Name == name);
		});

	// The cell's edit box, then Enter
	private void Type(WatchlistColumnItemViewModel column, string symbol, string text) =>
		TestUiThread.Invoke(() => _watchlist.SetCell(Row(symbol), column.Id, text));

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

	private string[] Symbols() => _watchlist.Rows.Select(x => x.Symbol).ToArray();

	private WatchlistRowViewModel Row(string symbol) =>
		_watchlist.Rows.Single(x => x.Symbol == symbol);
}
