using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Indicators.Scripting;
using LT.Indicators.Scripting.BuiltIns;
using LT.Indicators.Sdk;

namespace LT.Tests.Indicators;

public class BuiltInIndicatorTests
{
	private readonly IndicatorEngine _engine = new();

	// Two New York sessions of 5-minute bars: 26 Oct 09:30 … and 27 Oct 09:30 … (EDT, UTC-4)
	private static List<BarDto> TwoSessions()
	{
		var bars = new List<BarDto>();
		foreach (var day in new[] { 26, 27 })
			for (var i = 0; i < 40; i++)
				bars.Add(
					new BarDto
					{
						Time = new DateTimeOffset(
							2026,
							10,
							day,
							13,
							30,
							0,
							TimeSpan.Zero
						).AddMinutes(5 * i),
						Open = 100 + i,
						High = 101 + i,
						Low = 99 + i,
						Close = 100 + i + (i % 3 - 1) * 0.5,
						Volume = 100 + 10 * i,
					}
				);
		return bars;
	}

	private IndicatorResultDto Run(string key, Dictionary<string, double>? inputs = null) =>
		_engine
			.Compute(
				TwoSessions(),
				true,
				[
					new IndicatorRunDto
					{
						DefinitionId = IndicatorHelpers.GetBuiltInId(key),
						Inputs = inputs ?? [],
					},
				],
				TimeSpan.FromSeconds(5)
			)
			.Single();

	[Fact]
	public void Every_built_in_computes_over_the_bars()
	{
		foreach (var (key, _) in BuiltInIndicators.All)
		{
			var result = Run(key);
			Assert.Null(result.Error);
			Assert.NotEmpty(result.Plots);
			Assert.All(
				result.Plots.Where(x => x.Kind != IndicatorPlotKind.Level),
				x => Assert.Equal(80, x.Values.Length)
			);
		}
	}

	[Fact]
	public void Rsi_has_its_own_pane_with_the_70_and_30_levels()
	{
		var result = Run("rsi");

		Assert.Equal(IndicatorPane.Separate, result.Pane);
		Assert.Equal(
			[70d, 50d, 30d],
			result.Plots.Where(x => x.Kind == IndicatorPlotKind.Level).Select(x => x.Level)
		);
		var rsi = result.Plots.Single(x => x.Name == "RSI").Values;
		Assert.Equal(Ta.Rsi(TwoSessions().Select(x => x.Close).ToArray(), 14).ToArray(), rsi);
	}

	[Fact]
	public void Macd_draws_a_histogram_under_its_two_lines()
	{
		var result = Run(
			"macd",
			new()
			{
				["Fast"] = 3,
				["Slow"] = 6,
				["Signal"] = 4,
			}
		);

		Assert.Equal(
			[
				IndicatorPlotKind.Histogram,
				IndicatorPlotKind.Line,
				IndicatorPlotKind.Line,
				IndicatorPlotKind.Level,
			],
			result.Plots.Select(x => x.Kind)
		);
		var histogram = result.Plots[0];
		Assert.NotNull(histogram.Color2);
		Assert.Equal(
			result.Plots[1].Values[^1] - result.Plots[2].Values[^1],
			histogram.Values[^1],
			9
		);
	}

	[Fact]
	public void Bollinger_bands_are_a_band_around_the_basis()
	{
		var result = Run("bb");

		var band = result.Plots.Single(x => x.Kind == IndicatorPlotKind.Band);
		var basis = result.Plots.Single(x => x.Name == "Basis").Values;
		Assert.True(band.Values[^1] > basis[^1] && band.Values2![^1] < basis[^1]);
		Assert.Equal(basis[^1] - band.Values2![^1], band.Values[^1] - basis[^1], 9);
	}

	[Fact]
	public void Vwap_restarts_at_the_first_bar_of_each_new_york_day()
	{
		var bars = TwoSessions();
		var vwap = Run("vwap").Plots.Single().Values;

		var first = bars[40];
		Assert.Equal((first.High + first.Low + first.Close) / 3, vwap[40], 9);
		Assert.NotEqual((bars[39].High + bars[39].Low + bars[39].Close) / 3, vwap[39], 9);
	}

	[Fact]
	public void Atr_and_volume_ma_have_their_panes()
	{
		Assert.Equal(IndicatorPane.Separate, Run("atr").Pane);
		var volume = Run("volume-ma", new() { ["Length"] = 2 });
		Assert.Equal(IndicatorPane.Volume, volume.Pane);
		Assert.Equal(105, volume.Plots.Single().Values[1], 9);
	}
}
