using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Indicators.Scripting;

namespace LT.Tests.Indicators;

public class RelativeVolumeIndicatorTests
{
	private readonly IndicatorEngine _engine = new();

	// New York session opens in UTC: 29 and 30 Oct 2026 are EDT (UTC-4), 2 Nov is EST (UTC-5)
	// after the DST change, so 09:30 there is 14:30 UTC instead of 13:30
	private static readonly DateTimeOffset[] Opens =
	[
		new(2026, 10, 29, 13, 30, 0, TimeSpan.Zero),
		new(2026, 10, 30, 13, 30, 0, TimeSpan.Zero),
		new(2026, 11, 2, 14, 30, 0, TimeSpan.Zero),
		new(2026, 11, 3, 14, 30, 0, TimeSpan.Zero),
	];

	// 5-minute bars from 09:30 New York, one volume list per session
	private static List<BarDto> Sessions(params double[][] volumes) =>
		[
			.. volumes.SelectMany(
				(session, s) =>
					session.Select((volume, i) => Bar(Opens[s].AddMinutes(5 * i), volume))
			),
		];

	private static BarDto Bar(DateTimeOffset time, double volume) =>
		new()
		{
			Time = time,
			Open = 100,
			High = 101,
			Low = 99,
			Close = 100,
			Volume = (decimal)volume,
		};

	private IndicatorResultDto Run(
		List<BarDto> bars,
		int days,
		bool cumulative,
		bool isIntraday = true
	) =>
		_engine
			.Compute(
				bars,
				isIntraday,
				[
					new IndicatorRunDto
					{
						DefinitionId = IndicatorHelpers.GetBuiltInId("rvol"),
						Inputs = new() { ["Days"] = days, ["Cumulative"] = cumulative ? 1 : 0 },
					},
				],
				TimeSpan.FromSeconds(5)
			)
			.Single();

	private static double[] Values(IndicatorResultDto result, string plot) =>
		result.Plots.Single(x => x.Name == plot).Values;

	[Fact]
	public void Exact_mode_averages_the_same_time_of_day_over_the_previous_sessions()
	{
		var result = Run(Sessions([100, 200, 300], [300, 400, 500], [200, 900, 100]), 2, false);

		Assert.Null(result.Error);
		Assert.Equal(IndicatorPane.Separate, result.Pane);
		Assert.Equal(
			[double.NaN, double.NaN, double.NaN, 100, 200, 300, 200, 300, 400],
			Values(result, "Average")
		);
		// Green only above the average; at or below it, and while there is none, grey
		Assert.Equal(
			[double.NaN, double.NaN, double.NaN, 300, 400, 500, double.NaN, 900, double.NaN],
			Values(result, "Above average")
		);
		Assert.Equal(
			[100, 200, 300, double.NaN, double.NaN, double.NaN, 200, double.NaN, 100],
			Values(result, "Volume")
		);
	}

	[Fact]
	public void Exact_mode_keeps_only_the_last_days_sessions()
	{
		var result = Run(Sessions([100], [300], [500], [0]), 2, false);

		Assert.Equal([double.NaN, 100, 200, 400], Values(result, "Average"));
	}

	[Fact]
	public void A_session_without_the_time_of_day_is_left_out_of_its_average()
	{
		// The second session ends after two bars (a half day)
		var result = Run(Sessions([100, 200, 300], [300, 400], [0, 0, 0]), 2, false);

		Assert.Equal(
			[double.NaN, double.NaN, double.NaN, 100, 200, 200, 300, 300],
			Values(result, "Average")
		);
	}

	[Fact]
	public void Cumulative_mode_compares_running_totals_that_restart_each_session()
	{
		var result = Run(Sessions([100, 200, 300], [300, 400, 500], [200, 900, 100]), 2, true);

		Assert.Equal(
			[IndicatorPlotKind.Line, IndicatorPlotKind.Line],
			result.Plots.Select(x => x.Kind)
		);
		Assert.Equal([100, 300, 600, 300, 700, 1200, 200, 1100, 1200], Values(result, "Today"));
		Assert.Equal(
			[double.NaN, double.NaN, double.NaN, 100, 300, 600, 200, 500, 900],
			Values(result, "Average")
		);
	}

	[Fact]
	public void Daily_bars_average_the_previous_bars_even_in_cumulative_mode()
	{
		var days = Enumerable
			.Range(0, 4)
			.Select(i => Bar(new DateTimeOffset(2026, 10, 26 + i, 0, 0, 0, TimeSpan.Zero), 0))
			.ToList();
		double[] volumes = [100, 200, 300, 600];
		for (var i = 0; i < days.Count; i++)
			days[i].Volume = (decimal)volumes[i];

		var result = Run(days, 2, true, isIntraday: false);

		Assert.DoesNotContain(result.Plots, x => x.Name == "Today");
		Assert.Equal([double.NaN, 100, 150, 250], Values(result, "Average"));
		Assert.Equal([double.NaN, 200, 300, 600], Values(result, "Above average"));
	}

	[Fact]
	public void It_computes_over_no_bars()
	{
		var result = Run([], 10, true);

		Assert.Null(result.Error);
		Assert.All(result.Plots, x => Assert.Empty(x.Values));
	}
}
