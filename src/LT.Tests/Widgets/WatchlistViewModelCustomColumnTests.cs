using System.ComponentModel;
using LT.App.Stores;
using LT.App.Widgets.Watchlist;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// The watchlist widget's custom columns (add, rename, type values, sort, delete) and importing a
/// file into it. Disconnected, so no quotes are asked for.
/// </summary>
public class WatchlistViewModelCustomColumnTests : IDisposable
{
	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly WatchlistManager _watchlists;
	private readonly List<WatchlistViewModel> _widgets = [];
	private readonly WatchlistViewModel _watchlist;

	public WatchlistViewModelCustomColumnTests()
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
	public void An_added_column_opens_its_name_and_keeps_the_typed_one()
	{
		var column = AddColumn("Target");

		Assert.Equal(["Target"], _watchlist.CustomColumns.Select(x => x.Name));
		Assert.Equal(
			["Target"],
			_watchlists.GetColumns(_watchlists.GetActive().Id).Select(x => x.Name)
		);
		Assert.All(
			_watchlist.Rows,
			row => Assert.Equal(column.Id, Assert.Single(row.Cells).ColumnId)
		);
	}

	[Fact]
	public void Typed_values_are_saved_and_sort_numbers_by_value_then_text_blanks_last()
	{
		var column = AddColumn("Target");
		Type(column, "SPY", "100");
		Type(column, "AAPL", "hold");
		Type(column, "NVDA", " 9.5 ");

		Assert.Equal("9.5", Row("NVDA").GetCustomValue(column.Id));
		TestUiThread.Invoke(() => _watchlist.SortByCustom(column.Id));
		Assert.Equal(["NVDA", "SPY", "AAPL", "MSFT"], Symbols());

		TestUiThread.Invoke(() => _watchlist.SortByCustom(column.Id));
		Assert.Equal(ListSortDirection.Descending, _watchlist.SortDirection);
		Assert.Equal(["AAPL", "SPY", "NVDA", "MSFT"], Symbols());

		// A new value re-sorts at once
		Type(column, "MSFT", "500");
		Assert.Equal(["AAPL", "MSFT", "SPY", "NVDA"], Symbols());

		// Saved: a reopened widget has the values
		Assert.Equal(
			"500",
			Create().Rows.Single(x => x.Symbol == "MSFT").GetCustomValue(column.Id)
		);
	}

	[Fact]
	public void Deleting_the_sorted_column_goes_back_to_the_saved_order()
	{
		var column = AddColumn("Target");
		Type(column, "MSFT", "1");
		TestUiThread.Invoke(() => _watchlist.SortByCustom(column.Id));
		Assert.Equal("MSFT", Symbols()[0]);

		TestUiThread.Invoke(() => _watchlist.ConfirmDeleteColumnCommand.Execute(column));

		Assert.Empty(_watchlist.CustomColumns);
		Assert.Equal(WatchlistSortColumn.None, _watchlist.SortColumn);
		Assert.Equal(["SPY", "AAPL", "MSFT", "NVDA"], Symbols());
		Assert.All(_watchlist.Rows, row => Assert.Empty(row.Cells));
	}

	[Fact]
	public void Another_widget_on_the_same_list_follows_columns_and_values()
	{
		var other = Create();
		var column = AddColumn("Note");
		Type(column, "AAPL", "earnings");

		Assert.Equal(["Note"], other.CustomColumns.Select(x => x.Name));
		Assert.Equal(
			"earnings",
			other.Rows.Single(x => x.Symbol == "AAPL").GetCustomValue(column.Id)
		);
	}

	[Fact]
	public void Importing_an_export_shows_the_new_list_with_its_columns()
	{
		var column = AddColumn("Target");
		Type(column, "NVDA", "1000");
		var json = TestUiThread.Invoke(_watchlist.ExportWatchlist);

		TestUiThread.Invoke(() => _watchlist.ImportAsync(json, "us.json").GetAwaiter().GetResult());

		Assert.Null(_watchlist.ErrorMessage);
		Assert.Equal("Main (2)", _watchlist.ActiveWatchlist?.Name);
		Assert.Equal(["SPY", "AAPL", "MSFT", "NVDA"], Symbols());
		Assert.Equal(["Target"], _watchlist.CustomColumns.Select(x => x.Name));
		Assert.Equal("1000", Row("NVDA").GetCustomValue(_watchlist.CustomColumns[0].Id));
		Assert.Contains("Imported Main (2) (4 symbols)", _watchlist.StatusMessage);
	}

	[Fact]
	public void A_symbol_list_needs_the_connection()
	{
		var before = _watchlists.GetWatchlists().Count;

		TestUiThread.Invoke(() =>
			_watchlist.ImportAsync("AAPL\nMSFT", "x.txt").GetAwaiter().GetResult()
		);

		Assert.Equal("Connect to IB to import a list of symbols.", _watchlist.ErrorMessage);
		Assert.Equal(before, _watchlists.GetWatchlists().Count);
		Assert.Equal(0, _ibkr.SearchCalls);
	}

	[Fact]
	public void An_invalid_file_shows_its_message_and_keeps_the_list()
	{
		TestUiThread.Invoke(() =>
			_watchlist.ImportAsync("{ broken", "x.json").GetAwaiter().GetResult()
		);

		Assert.Equal(
			"This file is not a watchlist file or a list of symbols.",
			_watchlist.ErrorMessage
		);
		Assert.Equal(4, _watchlist.Rows.Count);
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
				new MarketDataManager(_ibkr),
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
		_widgets.Add(widget);
		return widget;
	}

	// "+ Add column", then the typed name and Enter
	private WatchlistColumnItemViewModel AddColumn(string name) =>
		TestUiThread.Invoke(() =>
		{
			_watchlist.AddColumnCommand.Execute(null);
			var item = _watchlist.CustomColumns[^1];
			Assert.True(item.IsRenaming);
			item.RenameText = name;
			_watchlist.CommitRenameColumnCommand.Execute(item);
			return _watchlist.CustomColumns.Single(x => x.Name == name);
		});

	// The cell's edit box, then Enter
	private void Type(WatchlistColumnItemViewModel column, string symbol, string text) =>
		TestUiThread.Invoke(() => _watchlist.SetCell(Row(symbol), column.Id, text));

	private string[] Symbols() => _watchlist.Rows.Select(x => x.Symbol).ToArray();

	private WatchlistRowViewModel Row(string symbol) =>
		_watchlist.Rows.Single(x => x.Symbol == symbol);
}
