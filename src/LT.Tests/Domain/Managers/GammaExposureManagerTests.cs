using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.GammaExposure;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

public class GammaExposureManagerTests
{
	private const string Expiry = "20261120";
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 23, 15, 0, 0, TimeSpan.Zero)
	);
	private readonly MarketDataManager _marketData;

	private static readonly ContractDto Spy = new()
	{
		ConId = 756733,
		Symbol = "SPY",
		SecurityType = "STK",
		Currency = "USD",
	};

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public GammaExposureManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_marketData = new MarketDataManager(_ibkr, TimeProvider.System);
		_ibkr.SearchResults =
		[
			.. new[] { 90.0, 95, 100, 105, 110 }.SelectMany(x =>
				new[] { Option(x, "C"), Option(x, "P") }
			),
		];
		_ibkr.TicksFor = contract =>
			contract.SecurityType == "STK"
				? [new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 100 }]
				: OptionTicks(contract, 0.05, contract.Right == "C" ? 1000 : 2000);
	}

	private GammaExposureManager Manager() =>
		new(
			new OptionChainManager(
				_ibkr,
				_marketData,
				new CacheManager(
					new InMemoryCacheRepository(),
					new InMemorySettingsRepository(),
					_time
				),
				_time
			),
			_marketData,
			_time
		);

	private static GetGammaExposureRequest Request(int around = 5) =>
		new()
		{
			Underlying = Spy,
			Expiration = Expiry,
			StrikesAroundSpot = around,
		};

	private static ContractDetailsDto Option(double strike, string right) =>
		new()
		{
			Contract = new ContractDto
			{
				ConId = (int)strike * 10 + (right == "C" ? 1 : 2),
				Symbol = "SPY",
				SecurityType = "OPT",
				LastTradeDateOrContractMonth = Expiry,
				Strike = strike,
				Right = right,
				Multiplier = "100",
			},
		};

	// As recorded: open interest on the option's own side (the other 0), then the model greeks
	private static List<MarketDataTickDto> OptionTicks(
		ContractDto option,
		double gamma,
		decimal openInterest
	) =>
		[
			new()
			{
				Field = TickTypeHelpers.CallOpenInterest,
				Size = option.Right == "C" ? openInterest : 0,
			},
			new()
			{
				Field = TickTypeHelpers.PutOpenInterest,
				Size = option.Right == "P" ? openInterest : 0,
			},
			new()
			{
				Field = TickTypeHelpers.ModelOptionComputation,
				Option = new OptionComputationDto { Gamma = gamma, ImpliedVolatility = 0.2 },
			},
		];

	[Fact]
	public async Task Reads_each_option_and_sums_the_exposure()
	{
		var exposure = await Manager().GetGammaExposureAsync(Request(), cancellationToken: Ct);

		Assert.Equal(100, exposure.SpotPrice);
		Assert.Equal([90d, 95d, 100d, 105d, 110d], exposure.Strikes.Select(x => x.Strike));
		// Per strike: calls 0.05 × 1,000 × 100 × 100² × 1%, puts the same with 2,000
		Assert.All(exposure.Strikes, x => Assert.Equal(500_000, x.CallExposure, 6));
		Assert.All(exposure.Strikes, x => Assert.Equal(-1_000_000, x.PutExposure, 6));
		Assert.Equal(-2_500_000, exposure.NetExposure, 6);
		Assert.Equal((10, 10), (exposure.OptionsRequested, exposure.OptionsReceived));
	}

	[Fact]
	public async Task Options_stream_with_open_interest_and_give_their_lines_back()
	{
		await Manager().GetGammaExposureAsync(Request(), cancellationToken: Ct);

		var options = _ibkr
			.MarketDataRequested.Select(
				(x, i) => (Contract: x, Ticks: _ibkr.GenericTicksRequested[i])
			)
			.Where(x => x.Contract.SecurityType == "OPT")
			.ToList();
		Assert.Equal(10, options.Count);
		Assert.All(
			options,
			x => Assert.Equal(MarketDataConstants.OpenInterestGenericTick, x.Ticks)
		);
		Assert.Equal(0, _marketData.ActiveLines);
	}

	[Fact]
	public async Task Only_the_strikes_around_the_spot_are_read()
	{
		var exposure = await Manager()
			.GetGammaExposureAsync(Request(around: 1), cancellationToken: Ct);

		Assert.Equal([95d, 100d, 105d], exposure.Strikes.Select(x => x.Strike));
		Assert.Equal(6, exposure.OptionsRequested);
	}

	[Fact]
	public async Task An_option_without_an_answer_is_left_out()
	{
		_ibkr.TicksFor = contract =>
			contract.SecurityType == "STK"
				? [new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 100 }]
			: contract.Strike == 95 && contract.Right == "P" ? []
			: OptionTicks(contract, 0.05, 1000);

		var exposure = await Manager().GetGammaExposureAsync(Request(), cancellationToken: Ct);

		Assert.Equal((10, 9), (exposure.OptionsRequested, exposure.OptionsReceived));
		Assert.Equal(0, exposure.Strikes[1].PutExposure);
	}

	[Fact]
	public async Task Ib_refusing_every_option_throws_its_error()
	{
		_ibkr.MarketDataExceptionFor = contract =>
			contract.SecurityType == "OPT" ? new IbkrRequestException(354, "Not subscribed") : null;

		var error = await Assert.ThrowsAsync<IbkrRequestException>(() =>
			Manager().GetGammaExposureAsync(Request(), cancellationToken: Ct)
		);

		Assert.Equal(354, error.Code);
	}

	[Fact]
	public async Task No_free_lines_throws_before_asking()
	{
		for (
			var i = 0;
			i < MarketDataConstants.MaxLines - GammaExposureConstants.ReservedLines;
			i++
		)
			_marketData.AcquireLine(MarketDataStreamKind.News, Spy);

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			Manager().GetGammaExposureAsync(Request(), cancellationToken: Ct)
		);
		Assert.DoesNotContain(_ibkr.MarketDataRequested, x => x.SecurityType == "OPT");
	}

	[Theory]
	[InlineData("202611", 5)]
	[InlineData(Expiry, 0)]
	[InlineData(Expiry, GammaExposureConstants.MaxStrikesAroundSpot + 1)]
	public async Task Bad_requests_are_refused(string expiration, int around)
	{
		var request = Request(around);
		request.Expiration = expiration;

		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			Manager().GetGammaExposureAsync(request, cancellationToken: Ct)
		);
	}
}
