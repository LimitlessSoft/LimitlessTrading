using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.GammaExposure;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class GammaExposureHelpersTests
{
	private static GammaExposureOptionDto Option(
		double strike,
		string right,
		double? gamma,
		decimal? openInterest,
		double? volatility = 0.2
	) =>
		new()
		{
			Strike = strike,
			Right = right,
			Gamma = gamma,
			OpenInterest = openInterest,
			ImpliedVolatility = volatility,
		};

	[Theory]
	[InlineData(0, 20)]
	[InlineData(65, 15)]
	[InlineData(80, 0)]
	[InlineData(95, 0)]
	public void Line_budget_leaves_the_reserved_lines_free(int active, int budget) =>
		Assert.Equal(budget, GammaExposureHelpers.GetLineBudget(active));

	[Fact]
	public void Exposure_is_dollars_per_one_percent_move()
	{
		// 0.05 gamma × 1,000 contracts × 100 shares × 100² × 1%
		Assert.Equal(500_000, GammaExposureHelpers.GetExposure(0.05, 1000, 100, 100), 6);
	}

	[Fact]
	public void Calls_count_positive_and_puts_negative_per_strike()
	{
		var strikes = GammaExposureHelpers.GetStrikeExposures(
			[
				Option(105, "C", 0.03, 500),
				Option(100, "C", 0.05, 1000),
				Option(100, "P", 0.04, 2000),
				// No open interest: left out
				Option(105, "P", 0.02, null),
			],
			100,
			100
		);

		Assert.Equal([100d, 105d], strikes.Select(x => x.Strike));
		Assert.Equal(500_000, strikes[0].CallExposure, 6);
		Assert.Equal(-800_000, strikes[0].PutExposure, 6);
		Assert.Equal(-300_000, strikes[0].NetExposure, 6);
		Assert.Equal((1000m, 2000m), (strikes[0].CallOpenInterest, strikes[0].PutOpenInterest));
		Assert.Equal(0, strikes[1].PutExposure);
	}

	[Fact]
	public void Black_scholes_gamma_at_the_money()
	{
		// d1 = 0.1, φ(0.1) / (S σ √T)
		var expected = Math.Exp(-0.005) / Math.Sqrt(2 * Math.PI) / 20;

		Assert.Equal(expected, GammaExposureHelpers.BlackScholesGamma(100, 100, 0.2, 1), 12);
		Assert.Equal(0, GammaExposureHelpers.BlackScholesGamma(100, 100, 0, 1));
	}

	[Fact]
	public void Flip_lies_between_put_heavy_and_call_heavy_strikes()
	{
		var flip = GammaExposureHelpers.FindGammaFlip(
			[Option(90, "P", 0.01, 10_000), Option(110, "C", 0.01, 10_000)],
			100,
			100,
			30 / 365.0
		);

		Assert.NotNull(flip);
		Assert.InRange(flip.Value, 95, 105);
	}

	[Fact]
	public void No_flip_when_the_exposure_never_changes_sign()
	{
		Assert.Null(
			GammaExposureHelpers.FindGammaFlip(
				[Option(90, "C", 0.01, 10_000), Option(110, "C", 0.01, 10_000)],
				100,
				100,
				30 / 365.0
			)
		);
	}

	[Fact]
	public void Open_interest_is_the_options_own_side()
	{
		var quote = new QuoteDto
		{
			ModelGreeks = new OptionComputationDto { Gamma = 0.1 },
			CallOpenInterest = 0,
			PutOpenInterest = 41057,
		};

		Assert.Equal(41057m, GammaExposureHelpers.GetOpenInterest(quote, "P"));
		Assert.True(GammaExposureHelpers.IsComplete(quote, "P"));
		Assert.False(GammaExposureHelpers.IsComplete(new QuoteDto { PutOpenInterest = 1 }, "P"));
	}

	[Theory]
	[InlineData("100", 100)]
	[InlineData("10", 10)]
	[InlineData("", GammaExposureConstants.DefaultMultiplier)]
	public void Multiplier_from_the_contract_or_the_default(string multiplier, double expected) =>
		Assert.Equal(
			expected,
			GammaExposureHelpers.GetMultiplier(new ContractDto { Multiplier = multiplier })
		);

	[Theory]
	[InlineData(1_250_000_000, "$1.25B")]
	[InlineData(-340_000_000, "-$340M")]
	[InlineData(12_500, "$12.5K")]
	public void Exposure_labels_are_compact(double value, string text) =>
		Assert.Equal(text, GammaExposureHelpers.FormatExposure(value));
}
