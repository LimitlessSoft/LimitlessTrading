using LT.App.Widgets.Settings;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>Settings → General → Ticker settings: adding, removing and what Save writes.</summary>
public class TickerSettingsSettingViewModelTests
{
	private static readonly ContractDto Spy = new()
	{
		ConId = 756733,
		Symbol = "SPY",
		SecurityType = "STK",
		Exchange = "SMART",
		PrimaryExchange = "ARCA",
		Currency = "USD",
	};

	private static readonly ContractDto SpyCall = new()
	{
		ConId = 812345678,
		Symbol = "SPY",
		SecurityType = "OPT",
		Exchange = "SMART",
		Currency = "USD",
		LastTradeDateOrContractMonth = "20261120",
		Strike = 600,
		Right = "C",
	};

	private readonly FakeContractManager _contracts = new();

	private TickerSettingsSettingViewModel Create(params TickerSettingsDto[] saved) =>
		new(_contracts, saved, "Chart live updates per ticker");

	[Fact]
	public void Saved_entries_show_with_their_mode_and_save_back_unchanged()
	{
		var vm = Create(
			new TickerSettingsDto
			{
				Contract = Spy,
				ChartLiveUpdateMode = ChartLiveUpdateMode.TickByTickThrottled,
			}
		);

		var entry = Assert.Single(vm.Entries);
		Assert.Equal(("SPY", "STK · ARCA · USD"), (entry.Name, entry.Details));
		Assert.Equal(ChartLiveUpdateMode.TickByTickThrottled, entry.Mode?.Mode);
		var saved = Assert.Single(vm.ToDtos());
		Assert.Equal(
			(Spy.ConId, ChartLiveUpdateMode.TickByTickThrottled),
			(saved.Contract.ConId, saved.ChartLiveUpdateMode)
		);
	}

	[Fact]
	public void An_option_is_its_own_entry_apart_from_its_underlying()
	{
		var vm = Create();

		vm.Add(Spy);
		vm.Add(SpyCall);
		vm.Entries[1].Mode = ChartLiveUpdateOption.Find(ChartLiveUpdateMode.Quotes);

		Assert.Equal(
			[
				(Spy.ConId, ChartLiveUpdateMode.TickByTick),
				(SpyCall.ConId, ChartLiveUpdateMode.Quotes),
			],
			vm.ToDtos().Select(x => (x.Contract.ConId, x.ChartLiveUpdateMode))
		);
		Assert.Equal("SPY 20261120 600 C", vm.Entries[1].Name);
		Assert.True(vm.HasEntries);
		Assert.Null(vm.Status);
	}

	[Fact]
	public void A_ticker_already_listed_is_not_added_twice()
	{
		var vm = Create();
		vm.Add(Spy);

		vm.Add(new ContractDto { ConId = Spy.ConId, Symbol = "SPY" });

		Assert.Single(vm.Entries);
		Assert.Contains("already", vm.Status);
	}

	[Fact]
	public async Task A_typed_ticker_resolves_to_its_listing_and_an_unknown_one_is_reported()
	{
		_contracts.Symbols["SPY"] = Spy;
		var vm = Create();

		await vm.AddTickerAsync(" spy ");
		await vm.AddTickerAsync("NOPE");

		Assert.Equal(Spy.ConId, Assert.Single(vm.Entries).Contract.ConId);
		Assert.False(string.IsNullOrEmpty(vm.Status));
	}

	[Fact]
	public void Removing_an_entry_drops_it_from_what_save_writes()
	{
		var vm = Create();
		vm.Add(Spy);
		vm.Add(SpyCall);

		vm.Entries[0].RemoveCommand.Execute(null);

		Assert.Equal(SpyCall.ConId, Assert.Single(vm.ToDtos()).Contract.ConId);
	}
}
