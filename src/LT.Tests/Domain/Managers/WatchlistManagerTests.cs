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
	private readonly WatchlistManager _manager;

	public WatchlistManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new WatchlistManager(_settings);
	}

	[Fact]
	public void Add_persists_and_get_returns_the_contract()
	{
		_manager.Add(
			new ContractDto
			{
				ConId = 1,
				Symbol = "AAPL",
				Exchange = "SMART",
			}
		);

		var item = Assert.Single(_manager.GetItems());
		Assert.Equal(("AAPL", "SMART"), (item.Symbol, item.Exchange));
	}

	[Fact]
	public void Duplicate_is_rejected()
	{
		_manager.Add(
			new ContractDto
			{
				ConId = 1,
				Symbol = "AAPL",
				SecurityType = "STK",
			}
		);

		var ex = Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.Add(
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
			_manager.Add(new ContractDto { Symbol = "AAPL" })
		);

	[Fact]
	public void Remove_deletes_by_con_id()
	{
		_manager.Add(new ContractDto { ConId = 1, Symbol = "AAPL" });
		_manager.Add(new ContractDto { ConId = 2, Symbol = "MSFT" });

		_manager.Remove(1);

		Assert.Equal(["MSFT"], _manager.GetItems().Select(x => x.Symbol));
	}

	[Fact]
	public void Older_settings_move_into_a_main_watchlist()
	{
		_settings.Settings.Watchlist.Add(new ContractEntity { ConId = 7, Symbol = "SPY" });

		var watchlist = Assert.Single(_manager.GetWatchlists());

		Assert.Equal(("Main", 1), (watchlist.Name, watchlist.Count));
		Assert.Equal(["SPY"], _manager.GetItems().Select(x => x.Symbol));
		Assert.Empty(_settings.Settings.Watchlist);
	}

	[Fact]
	public void A_new_watchlist_becomes_active_and_holds_its_own_items()
	{
		_manager.Add(new ContractDto { ConId = 1, Symbol = "AAPL" });
		var main = _manager.GetActive();

		var tech = _manager.SaveName(new SaveWatchlistNameRequest { Name = " Tech " });
		_manager.Add(new ContractDto { ConId = 2, Symbol = "MSFT" });

		Assert.Equal(("Tech", tech.Id), (tech.Name, _manager.GetActive().Id));
		Assert.Equal(["MSFT"], _manager.GetItems().Select(x => x.Symbol));
		_manager.SetActive(main.Id);
		Assert.Equal(["AAPL"], _manager.GetItems().Select(x => x.Symbol));
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
		_manager.Add(new ContractDto { ConId = 1, Symbol = "AAPL" });
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
}
