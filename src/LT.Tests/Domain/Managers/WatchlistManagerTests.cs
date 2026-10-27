using System.Text.Json;
using LSCore.Exceptions;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Entities;
using LT.Contracts.Requests.Watchlists;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class WatchlistManagerTests
{
	private readonly InMemorySettingsRepository _settings = new();
	private readonly FakeContractManager _contracts = new();
	private readonly WatchlistManager _manager;

	public WatchlistManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new WatchlistManager(_settings, _contracts);
	}

	[Fact]
	public void Add_persists_and_get_returns_the_contract()
	{
		_manager.Add(
			_manager.GetActive().Id,
			new ContractDto
			{
				ConId = 1,
				Symbol = "AAPL",
				Exchange = "SMART",
			}
		);

		var item = Assert.Single(_manager.GetItems(_manager.GetActive().Id));
		Assert.Equal(("AAPL", "SMART"), (item.Symbol, item.Exchange));
	}

	[Fact]
	public void Duplicate_is_rejected()
	{
		_manager.Add(
			_manager.GetActive().Id,
			new ContractDto
			{
				ConId = 1,
				Symbol = "AAPL",
				SecurityType = "STK",
			}
		);

		var ex = Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.Add(
				_manager.GetActive().Id,
				new ContractDto
				{
					ConId = 1,
					Symbol = "AAPL",
					SecurityType = "STK",
				}
			)
		);
		Assert.Contains("AAPL", ex.Message);
	}

	[Fact]
	public void Unresolved_contract_is_rejected() =>
		Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.Add(_manager.GetActive().Id, new ContractDto { Symbol = "AAPL" })
		);

	[Fact]
	public void Remove_deletes_by_con_id()
	{
		_manager.Add(_manager.GetActive().Id, new ContractDto { ConId = 1, Symbol = "AAPL" });
		_manager.Add(_manager.GetActive().Id, new ContractDto { ConId = 2, Symbol = "MSFT" });

		_manager.Remove(_manager.GetActive().Id, new ContractDto { ConId = 1, Symbol = "AAPL" });

		Assert.Equal(["MSFT"], _manager.GetItems(_manager.GetActive().Id).Select(x => x.Symbol));
	}

	[Fact]
	public void Same_con_id_on_another_exchange_is_a_separate_item()
	{
		_manager.Add(
			_manager.GetActive().Id,
			new ContractDto
			{
				ConId = 479624278,
				Symbol = "BTC",
				SecurityType = "CRYPTO",
				Exchange = "PAXOS",
			}
		);
		_manager.Add(
			_manager.GetActive().Id,
			new ContractDto
			{
				ConId = 479624278,
				Symbol = "BTC",
				SecurityType = "CRYPTO",
				Exchange = "ZEROHASH",
			}
		);

		_manager.Remove(
			_manager.GetActive().Id,
			new ContractDto { ConId = 479624278, Exchange = "PAXOS" }
		);

		Assert.Equal(
			["ZEROHASH"],
			_manager.GetItems(_manager.GetActive().Id).Select(x => x.Exchange)
		);
	}

	[Fact]
	public void Older_settings_move_into_a_main_watchlist()
	{
		_settings.Settings.Watchlist.Add(new ContractEntity { ConId = 7, Symbol = "SPY" });

		var watchlist = Assert.Single(_manager.GetWatchlists());

		Assert.Equal(("Main", 1), (watchlist.Name, watchlist.Count));
		Assert.Equal(["SPY"], _manager.GetItems(_manager.GetActive().Id).Select(x => x.Symbol));
		Assert.Empty(_settings.Settings.Watchlist);
	}

	[Fact]
	public void A_new_watchlist_becomes_active_and_holds_its_own_items()
	{
		_manager.Add(_manager.GetActive().Id, new ContractDto { ConId = 1, Symbol = "AAPL" });
		var main = _manager.GetActive();

		var tech = _manager.SaveName(new SaveWatchlistNameRequest { Name = " Tech " });
		_manager.Add(_manager.GetActive().Id, new ContractDto { ConId = 2, Symbol = "MSFT" });

		Assert.Equal(("Tech", tech.Id), (tech.Name, _manager.GetActive().Id));
		Assert.Equal(["MSFT"], _manager.GetItems(_manager.GetActive().Id).Select(x => x.Symbol));
		_manager.SetActive(main.Id);
		Assert.Equal(["AAPL"], _manager.GetItems(_manager.GetActive().Id).Select(x => x.Symbol));
	}

	[Fact]
	public void Names_must_be_unique_ignoring_case()
	{
		_manager.SaveName(new SaveWatchlistNameRequest { Name = "Tech" });

		Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.SaveName(new SaveWatchlistNameRequest { Name = "tech" })
		);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("12345678901234567890123456789012345678901")]
	public void Blank_or_long_names_are_rejected(string name) =>
		Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.SaveName(new SaveWatchlistNameRequest { Name = name })
		);

	[Fact]
	public void Rename_keeps_the_items()
	{
		_manager.Add(_manager.GetActive().Id, new ContractDto { ConId = 1, Symbol = "AAPL" });
		var main = _manager.GetActive();

		_manager.SaveName(new SaveWatchlistNameRequest { Id = main.Id, Name = "Core" });

		Assert.Equal(("Core", 1), (_manager.GetActive().Name, _manager.GetActive().Count));
	}

	[Fact]
	public void The_last_watchlist_cant_be_deleted() =>
		Assert.Throws<LSCoreBadRequestException>(() => _manager.Delete(_manager.GetActive().Id));

	[Fact]
	public void Deleting_the_active_watchlist_activates_another()
	{
		var main = _manager.GetActive();
		var tech = _manager.SaveName(new SaveWatchlistNameRequest { Name = "Tech" });

		_manager.Delete(tech.Id);

		Assert.Equal(main.Id, _manager.GetActive().Id);
		Assert.Equal(["Main"], _manager.GetWatchlists().Select(x => x.Name));
	}

	[Fact]
	public void New_names_count_up_past_existing_ones()
	{
		_manager.SaveName(new SaveWatchlistNameRequest { Name = "Watchlist 2" });

		Assert.Equal("Watchlist 3", _manager.GetNewName());
	}

	[Fact]
	public void Pins_are_kept_in_the_order_they_were_made()
	{
		_manager.Add(_manager.GetActive().Id, new ContractDto { ConId = 1, Symbol = "AAPL" });
		_manager.Add(_manager.GetActive().Id, new ContractDto { ConId = 2, Symbol = "MSFT" });
		_manager.Add(_manager.GetActive().Id, new ContractDto { ConId = 3, Symbol = "NVDA" });

		_manager.Pin(_manager.GetActive().Id, new ContractDto { ConId = 3, Symbol = "NVDA" });
		_manager.Pin(_manager.GetActive().Id, new ContractDto { ConId = 1, Symbol = "AAPL" });
		_manager.Pin(_manager.GetActive().Id, new ContractDto { ConId = 3, Symbol = "NVDA" });

		Assert.Equal(
			["NVDA", "AAPL"],
			_manager.GetPinned(_manager.GetActive().Id).Select(x => x.Symbol)
		);
		// Items keep their saved order
		Assert.Equal(
			["AAPL", "MSFT", "NVDA"],
			_manager.GetItems(_manager.GetActive().Id).Select(x => x.Symbol)
		);
	}

	[Fact]
	public void Unpin_and_pin_again_puts_it_last()
	{
		_manager.Add(_manager.GetActive().Id, new ContractDto { ConId = 1, Symbol = "AAPL" });
		_manager.Add(_manager.GetActive().Id, new ContractDto { ConId = 2, Symbol = "MSFT" });
		_manager.Pin(_manager.GetActive().Id, new ContractDto { ConId = 1 });
		_manager.Pin(_manager.GetActive().Id, new ContractDto { ConId = 2 });

		_manager.Unpin(_manager.GetActive().Id, new ContractDto { ConId = 1 });
		Assert.Equal(["MSFT"], _manager.GetPinned(_manager.GetActive().Id).Select(x => x.Symbol));

		_manager.Pin(_manager.GetActive().Id, new ContractDto { ConId = 1 });
		Assert.Equal(
			["MSFT", "AAPL"],
			_manager.GetPinned(_manager.GetActive().Id).Select(x => x.Symbol)
		);
	}

	[Fact]
	public void Pins_are_per_listing()
	{
		_manager.Add(
			_manager.GetActive().Id,
			new ContractDto
			{
				ConId = 9,
				Symbol = "BTC",
				Exchange = "PAXOS",
			}
		);
		_manager.Add(
			_manager.GetActive().Id,
			new ContractDto
			{
				ConId = 9,
				Symbol = "BTC",
				Exchange = "ZEROHASH",
			}
		);

		_manager.Pin(_manager.GetActive().Id, new ContractDto { ConId = 9, Exchange = "ZEROHASH" });

		Assert.Equal(
			["ZEROHASH"],
			_manager.GetPinned(_manager.GetActive().Id).Select(x => x.Exchange)
		);
	}

	[Fact]
	public void Only_an_item_of_the_watchlist_can_be_pinned()
	{
		var ex = Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.Pin(_manager.GetActive().Id, new ContractDto { ConId = 1, Symbol = "AAPL" })
		);

		Assert.Contains("AAPL", ex.Message);
		Assert.Empty(_manager.GetPinned(_manager.GetActive().Id));
	}

	[Fact]
	public void Removing_an_item_drops_its_pin()
	{
		_manager.Add(_manager.GetActive().Id, new ContractDto { ConId = 1, Symbol = "AAPL" });
		_manager.Pin(_manager.GetActive().Id, new ContractDto { ConId = 1 });

		_manager.Remove(_manager.GetActive().Id, new ContractDto { ConId = 1 });
		_manager.Add(_manager.GetActive().Id, new ContractDto { ConId = 1, Symbol = "AAPL" });

		Assert.Empty(_manager.GetPinned(_manager.GetActive().Id));
		Assert.Empty(WatchlistActive().Pinned);
	}

	[Fact]
	public void Pins_belong_to_their_watchlist()
	{
		_manager.Add(_manager.GetActive().Id, new ContractDto { ConId = 1, Symbol = "AAPL" });
		_manager.Pin(_manager.GetActive().Id, new ContractDto { ConId = 1 });
		var main = _manager.GetActive();

		_manager.SaveName(new SaveWatchlistNameRequest { Name = "Tech" });
		_manager.Add(_manager.GetActive().Id, new ContractDto { ConId = 1, Symbol = "AAPL" });

		Assert.Empty(_manager.GetPinned(_manager.GetActive().Id));
		_manager.SetActive(main.Id);
		Assert.Equal(["AAPL"], _manager.GetPinned(_manager.GetActive().Id).Select(x => x.Symbol));
	}

	[Fact]
	public void A_pin_without_its_item_is_ignored()
	{
		_manager.Add(_manager.GetActive().Id, new ContractDto { ConId = 1, Symbol = "AAPL" });
		WatchlistActive().Pinned.Add(new WatchlistPinEntity { ConId = 99, Exchange = "SMART" });

		Assert.Empty(_manager.GetPinned(_manager.GetActive().Id));
	}

	[Fact]
	public void Settings_saved_before_pinning_load_with_nothing_pinned()
	{
		const string json = """
			{
			  "Watchlists": [
			    {
			      "Id": "w1",
			      "Name": "Main",
			      "Items": [ { "ConId": 1, "Symbol": "AAPL", "Exchange": "SMART" } ]
			    }
			  ],
			  "ActiveWatchlistId": "w1"
			}
			""";
		var settings = new InMemorySettingsRepository();
		settings.Save(JsonSerializer.Deserialize<SettingsEntity>(json)!);
		var manager = new WatchlistManager(settings, new FakeContractManager());

		Assert.Empty(manager.GetPinned("w1"));
		Assert.Equal(["AAPL"], manager.GetItems("w1").Select(x => x.Symbol));

		manager.Pin("w1", new ContractDto { ConId = 1, Exchange = "SMART" });
		Assert.Equal(["AAPL"], manager.GetPinned("w1").Select(x => x.Symbol));
	}

	[Fact]
	public void Pins_survive_a_save_and_load()
	{
		_manager.Add(
			_manager.GetActive().Id,
			new ContractDto
			{
				ConId = 1,
				Symbol = "AAPL",
				Exchange = "SMART",
			}
		);
		_manager.Add(
			_manager.GetActive().Id,
			new ContractDto
			{
				ConId = 2,
				Symbol = "MSFT",
				Exchange = "SMART",
			}
		);
		_manager.Pin(_manager.GetActive().Id, new ContractDto { ConId = 2, Exchange = "SMART" });
		_manager.Pin(_manager.GetActive().Id, new ContractDto { ConId = 1, Exchange = "SMART" });

		var reloaded = new InMemorySettingsRepository();
		reloaded.Save(
			JsonSerializer.Deserialize<SettingsEntity>(
				JsonSerializer.Serialize(_settings.Settings)
			)!
		);

		Assert.Equal(
			["MSFT", "AAPL"],
			new WatchlistManager(reloaded, new FakeContractManager())
				.GetPinned(_manager.GetActive().Id)
				.Select(x => x.Symbol)
		);
	}

	[Fact]
	public void Items_and_pins_are_changed_on_the_given_watchlist_whatever_is_active()
	{
		var mainId = _manager.GetActive().Id;
		var secondId = _manager.SaveName(new SaveWatchlistNameRequest { Name = "Second" }).Id;
		Assert.Equal(secondId, _manager.GetActive().Id);
		var aapl = new ContractDto
		{
			ConId = 1,
			Symbol = "AAPL",
			Exchange = "SMART",
		};

		_manager.Add(mainId, aapl);
		_manager.Pin(mainId, aapl);

		Assert.Equal(["AAPL"], _manager.GetItems(mainId).Select(x => x.Symbol));
		Assert.Equal(["AAPL"], _manager.GetPinned(mainId).Select(x => x.Symbol));
		Assert.Empty(_manager.GetItems(secondId));
		Assert.Empty(_manager.GetPinned(secondId));
	}

	[Fact]
	public void A_deleted_watchlist_can_no_longer_take_items()
	{
		var secondId = _manager.SaveName(new SaveWatchlistNameRequest { Name = "Second" }).Id;
		_manager.Delete(secondId);

		Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.Add(secondId, new ContractDto { ConId = 1, Symbol = "AAPL" })
		);
		Assert.Throws<LSCoreBadRequestException>(() => _manager.GetItems(secondId));
	}

	[Fact]
	public void Every_change_to_the_watchlists_raises_Changed()
	{
		var raised = 0;
		_manager.Changed += () => raised++;
		var id = _manager.GetActive().Id;
		var aapl = new ContractDto
		{
			ConId = 1,
			Symbol = "AAPL",
			Exchange = "SMART",
		};

		_manager.Add(id, aapl);
		_manager.Pin(id, aapl);
		_manager.Unpin(id, aapl);
		_manager.Remove(id, aapl);
		var secondId = _manager.SaveName(new SaveWatchlistNameRequest { Name = "Second" }).Id;
		_manager.Delete(secondId);
		Assert.Equal(6, raised);

		// Nothing changed: nothing raised
		_manager.Remove(id, aapl);
		_manager.Unpin(id, aapl);
		Assert.Equal(6, raised);
	}

	private WatchlistEntity WatchlistActive() =>
		_settings.Settings.Watchlists.First(x => x.Id == _settings.Settings.ActiveWatchlistId);
}
