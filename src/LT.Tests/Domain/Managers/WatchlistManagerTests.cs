using LSCore.Exceptions;
using LT.Contracts.Dtos.Contracts;
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
}
