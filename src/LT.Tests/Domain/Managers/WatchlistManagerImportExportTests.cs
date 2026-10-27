using System.Text.Json;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Requests.Watchlists;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

/// <summary>
/// Exporting a watchlist to JSON and importing it back, importing plain symbol lists (looked up
/// one at a time) and refusing files that are neither.
/// </summary>
public class WatchlistManagerImportExportTests
{
	private readonly InMemorySettingsRepository _settings = new();
	private readonly FakeContractManager _contracts = new();
	private readonly WatchlistManager _manager;
	private readonly string _id;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public WatchlistManagerImportExportTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new WatchlistManager(_settings, _contracts);
		_id = _manager.GetActive().Id;
	}

	[Fact]
	public async Task Export_and_import_round_trip_items_pins_columns_and_values()
	{
		_manager.SaveName(new SaveWatchlistNameRequest { Id = _id, Name = "Tech" });
		_manager.Add(_id, Stock(3, "NVDA"));
		_manager.Add(_id, Stock(1, "AAPL"));
		_manager.Add(_id, Stock(2, "MSFT", "NASDAQ"));
		_manager.Pin(_id, Stock(2, "MSFT", "NASDAQ"));
		_manager.Pin(_id, Stock(3, "NVDA"));
		var target = AddColumn("Target");
		var note = AddColumn("Note");
		SetCell(target, Stock(3, "NVDA"), "1,200.5");
		SetCell(note, Stock(1, "AAPL"), "earnings, Thursday");
		SetCell(note, Stock(2, "MSFT", "NASDAQ"), "\"quoted\"");

		var json = _manager.Export(_id);
		var result = await _manager.ImportAsync(json, "whatever.json", Ct);

		Assert.Equal("Tech (2)", result.Watchlist.Name);
		Assert.Equal(3, result.Watchlist.Count);
		Assert.Empty(result.NotFound);
		Assert.Equal(result.Watchlist.Id, _manager.GetActive().Id);
		var id = result.Watchlist.Id;
		Assert.Equal(
			[(3, "NVDA", "SMART"), (1, "AAPL", "SMART"), (2, "MSFT", "NASDAQ")],
			_manager.GetItems(id).Select(x => (x.ConId, x.Symbol, x.Exchange))
		);
		Assert.Equal(["MSFT", "NVDA"], _manager.GetPinned(id).Select(x => x.Symbol));
		var columns = _manager.GetColumns(id);
		Assert.Equal(["Target", "Note"], columns.Select(x => x.Name));
		Assert.NotEqual(target, columns[0].Id);
		Assert.Equal([(3, "1,200.5")], columns[0].Cells.Select(x => (x.ConId, x.Value)));
		Assert.Equal(
			[(1, "earnings, Thursday"), (2, "\"quoted\"")],
			columns[1].Cells.OrderBy(x => x.ConId).Select(x => (x.ConId, x.Value))
		);
		// The same file again exports the same (but its name)
		Assert.Equal(json.Replace("\"Tech\"", "\"Tech (2)\""), _manager.Export(id));
	}

	[Fact]
	public void Export_is_camel_case_json_of_our_format()
	{
		_manager.Add(_id, Stock(1, "AAPL"));

		using var doc = JsonDocument.Parse(_manager.Export(_id));

		Assert.Equal(
			WatchlistConstants.FileFormat,
			doc.RootElement.GetProperty("format").GetString()
		);
		Assert.Equal(
			WatchlistConstants.FileVersion,
			doc.RootElement.GetProperty("version").GetInt32()
		);
		var item = doc.RootElement.GetProperty("items")[0];
		Assert.Equal(1, item.GetProperty("contract").GetProperty("conId").GetInt32());
		Assert.False(item.TryGetProperty("pin", out _));
	}

	[Fact]
	public async Task A_hand_edited_file_with_any_case_comments_and_unknown_fields_imports()
	{
		var json = """
			{
			  // edited by hand
			  "Format": "LimitlessTrading.Watchlist",
			  "version": 7,
			  "name": "Mine",
			  "columns": ["Target"],
			  "items": [
			    { "contract": { "conId": 5, "symbol": "TSLA", "exchange": "SMART" }, "values": { "target": " 300 " }, "future": true },
			    { "contract": { "conId": 5, "symbol": "TSLA", "exchange": "SMART" } },
			  ],
			}
			""";

		var result = await _manager.ImportAsync(json, null, Ct);

		Assert.Equal("Mine", result.Watchlist.Name);
		Assert.Equal(["TSLA"], _manager.GetItems(result.Watchlist.Id).Select(x => x.Symbol));
		Assert.Equal("300", _manager.GetColumns(result.Watchlist.Id)[0].Cells[0].Value);
	}

	[Theory]
	[InlineData("{ not json", "not a watchlist file")]
	[InlineData(
		"{ \"format\": \"LimitlessTrading.ChartTemplates\", \"items\": [] }",
		"not a watchlist file"
	)]
	[InlineData("{ \"format\": \"LimitlessTrading.Watchlist\", \"items\": [] }", "no symbols")]
	[InlineData(
		"{ \"format\": \"LimitlessTrading.Watchlist\", \"items\": [ { \"contract\": { \"conId\": 1 } }, { \"contract\": { \"exchange\": \"SMART\" } } ] }",
		"Item 2 in the file has neither a contract id nor a symbol"
	)]
	[InlineData(
		"{ \"format\": \"LimitlessTrading.Watchlist\", \"items\": [ { } ] }",
		"Item 1 in the file has neither a contract id nor a symbol"
	)]
	[InlineData(
		"{ \"format\": \"LimitlessTrading.Watchlist\", \"items\": [ { \"contract\": { \"conId\": 0, \"symbol\": \"MS{FT}\" } } ] }",
		"MS{FT} is not a symbol"
	)]
	[InlineData(
		"{ \"format\": \"LimitlessTrading.Watchlist\", \"columns\": [\"A\", \"a\"], \"items\": [ { \"contract\": { \"conId\": 1 } } ] }",
		"A column named a already exists"
	)]
	[InlineData(
		"{ \"format\": \"LimitlessTrading.Watchlist\", \"columns\": [\" \"], \"items\": [ { \"contract\": { \"conId\": 1 } } ] }",
		"Column name is required"
	)]
	[InlineData("AAPL\u0000\u0001PNG", "not a watchlist file")]
	[InlineData("AAPL\nMS{FT}", "MS{FT} is not a symbol")]
	[InlineData(" \n# nothing\n", "no symbols")]
	public async Task An_invalid_file_is_refused_with_a_message_and_adds_nothing(
		string content,
		string message
	)
	{
		var before = _manager.GetWatchlists().Count;

		var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
			_manager.ImportAsync(content, "x", Ct)
		);

		Assert.Contains(message, ex.Message);
		Assert.Equal(before, _manager.GetWatchlists().Count);
		Assert.Empty(_contracts.Lookups);
	}

	[Fact]
	public async Task A_file_with_too_many_items_is_refused()
	{
		var symbols = string.Join(
			"\n",
			Enumerable.Range(0, MarketDataConstants.WatchlistMaxItems + 1).Select(i => $"S{i}")
		);

		var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
			_manager.ImportAsync(symbols, "x", Ct)
		);

		Assert.Contains($"at most {MarketDataConstants.WatchlistMaxItems}", ex.Message);
		Assert.Empty(_contracts.Lookups);
	}

	[Fact]
	public async Task A_file_larger_than_the_limit_is_refused()
	{
		var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
			_manager.ImportAsync(new string('A', WatchlistConstants.MaxFileLength + 1), "x", Ct)
		);

		Assert.Contains("larger than", ex.Message);
	}

	[Fact]
	public async Task A_symbol_list_is_looked_up_one_at_a_time_and_unknown_symbols_reported()
	{
		_contracts.Symbols["SPY"] = Stock(10, "SPY", "ARCA");
		_contracts.Symbols["AAPL"] = Stock(1, "AAPL");
		_contracts.Symbols["GOOG"] = Stock(1, "AAPL"); // same listing twice: kept once

		var result = await _manager.ImportAsync(
			"Symbol\nspy\nNOPE, AAPL\nGOOG\n",
			"my list.csv",
			Ct
		);

		Assert.Equal(["SPY", "NOPE", "AAPL", "GOOG"], _contracts.Lookups);
		Assert.Equal(1, _contracts.MaxConcurrent);
		Assert.Equal("my list", result.Watchlist.Name);
		Assert.Equal(["NOPE"], result.NotFound);
		Assert.Equal(["SPY", "AAPL"], _manager.GetItems(result.Watchlist.Id).Select(x => x.Symbol));
		Assert.Empty(_manager.GetColumns(result.Watchlist.Id));
	}

	[Fact]
	public async Task A_symbol_list_with_no_known_symbol_is_refused()
	{
		var before = _manager.GetWatchlists().Count;

		var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
			_manager.ImportAsync("FOO,BAR", "x.txt", Ct)
		);

		Assert.Contains("None of the symbols were found: FOO, BAR", ex.Message);
		Assert.Equal(before, _manager.GetWatchlists().Count);
	}

	[Fact]
	public async Task Cancelling_a_symbol_list_import_adds_nothing()
	{
		_contracts.Symbols["SPY"] = Stock(10, "SPY", "ARCA");
		var before = _manager.GetWatchlists().Count;

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			_manager.ImportAsync("SPY", "x.txt", new CancellationToken(true))
		);

		Assert.Equal(before, _manager.GetWatchlists().Count);
	}

	[Fact]
	public async Task File_items_without_a_conId_are_looked_up_one_at_a_time_with_their_pins_and_values()
	{
		_contracts.Found["AAPL"] = [Stock(265598, "AAPL")];
		var json = """
			{
			  "format": "LimitlessTrading.Watchlist",
			  "columns": ["Note"],
			  "items": [
			    { "contract": { "conId": 3, "symbol": "NVDA", "exchange": "SMART" } },
			    { "contract": { "symbol": "aapl", "securityType": "STK" }, "pin": 0, "values": { "Note": "found" } },
			    { "contract": { "conId": 0, "symbol": "NOPE" }, "values": { "Note": "lost" } },
			  ],
			}
			""";

		var result = await _manager.ImportAsync(json, "x.json", Ct);

		// Only the items without a conId asked IB, one at a time
		Assert.Equal(["aapl", "NOPE"], _contracts.Lookups);
		Assert.Equal(1, _contracts.MaxConcurrent);
		Assert.Equal(["NOPE"], result.NotFound);
		var id = result.Watchlist.Id;
		Assert.Equal(
			[(3, "NVDA"), (265598, "AAPL")],
			_manager.GetItems(id).Select(x => (x.ConId, x.Symbol))
		);
		Assert.Equal([265598], _manager.GetPinned(id).Select(x => x.ConId));
		Assert.Equal(
			[(265598, "found")],
			_manager.GetColumns(id)[0].Cells.Select(x => (x.ConId, x.Value))
		);
	}

	[Fact]
	public async Task A_file_option_without_a_conId_is_found_by_expiry_strike_and_right()
	{
		_contracts.Found["AAPL"] =
		[
			Option(1001, "20261218", 200, "C"),
			Option(1002, "20261218", 200, "P"),
			Option(1003, "20261218", 210, "C"),
			Option(1004, "20270115", 200, "C"),
		];
		var json = """
			{
			  "format": "LimitlessTrading.Watchlist",
			  "items": [
			    { "contract": { "symbol": "AAPL", "securityType": "OPT", "lastTradeDateOrContractMonth": "20261218", "strike": 200, "right": "CALL" } },
			    { "contract": { "symbol": "AAPL", "securityType": "OPT", "lastTradeDateOrContractMonth": "202612", "strike": 200 } },
			  ],
			}
			""";

		var result = await _manager.ImportAsync(json, "x.json", Ct);

		// The second is a call or a put: ambiguous, so it is reported rather than guessed
		Assert.Equal([1001], _manager.GetItems(result.Watchlist.Id).Select(x => x.ConId));
		Assert.Equal(["AAPL 202612 200"], result.NotFound);
	}

	[Fact]
	public async Task A_file_whose_items_without_a_conId_are_all_unknown_is_refused()
	{
		var before = _manager.GetWatchlists().Count;

		var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
			_manager.ImportAsync(
				"""{ "format": "LimitlessTrading.Watchlist", "items": [ { "contract": { "symbol": "FOO" } } ] }""",
				"x.json",
				Ct
			)
		);

		Assert.Contains("None of the symbols were found: FOO", ex.Message);
		Assert.Equal(before, _manager.GetWatchlists().Count);
	}

	private static ContractDto Option(int conId, string expiry, double strike, string right) =>
		new()
		{
			ConId = conId,
			Symbol = "AAPL",
			SecurityType = "OPT",
			Exchange = "SMART",
			Currency = "USD",
			LastTradeDateOrContractMonth = expiry,
			Strike = strike,
			Right = right,
			Multiplier = "100",
		};

	private string AddColumn(string name) =>
		_manager.SaveColumn(new SaveWatchlistColumnRequest { WatchlistId = _id, Name = name }).Id;

	private void SetCell(string column, ContractDto contract, string value) =>
		_manager.SetCell(
			new SetWatchlistCellRequest
			{
				WatchlistId = _id,
				ColumnId = column,
				Contract = contract,
				Value = value,
			}
		);

	private static ContractDto Stock(int conId, string symbol, string exchange = "SMART") =>
		new()
		{
			ConId = conId,
			Symbol = symbol,
			SecurityType = "STK",
			Exchange = exchange,
			Currency = "USD",
		};
}
