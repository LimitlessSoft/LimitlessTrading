using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class IndicatorChartHelpersTests
{
	[Fact]
	public void Panes_take_a_fifth_each_until_they_share_sixty_percent()
	{
		Assert.Equal(0, IndicatorChartHelpers.GetPaneHeight(500, 0));
		Assert.Equal(100, IndicatorChartHelpers.GetPaneHeight(500, 1), 9);
		Assert.Equal(100, IndicatorChartHelpers.GetPaneHeight(500, 3), 9);
		Assert.Equal(75, IndicatorChartHelpers.GetPaneHeight(500, 4), 9);
	}

	[Fact]
	public void The_range_covers_visible_lines_bands_levels_and_zero_of_histograms()
	{
		IndicatorPlotDto[] plots =
		[
			new() { Kind = IndicatorPlotKind.Line, Values = [50, 5, 6, double.NaN, 99] },
			new()
			{
				Kind = IndicatorPlotKind.Band,
				Values = [0, 8, 7, double.NaN, 0],
				Values2 = [0, 4, 3, double.NaN, 0],
			},
			new() { Kind = IndicatorPlotKind.Markers, Values = [1, 1, 1, 1, 1] },
		];

		Assert.Equal((3d, 8d), IndicatorChartHelpers.GetRange(plots, 1, 4));
		Assert.Equal(
			(0d, 10d),
			IndicatorChartHelpers.GetRange(
				[
					new IndicatorPlotDto { Kind = IndicatorPlotKind.Histogram, Values = [2, 3] },
					new IndicatorPlotDto { Kind = IndicatorPlotKind.Level, Level = 10 },
				],
				0,
				2
			)
		);
		Assert.Null(IndicatorChartHelpers.GetRange(plots[2..], 0, 5));
	}

	[Fact]
	public void A_fixed_scale_wins_over_autoscale()
	{
		var rsi = new IndicatorResultDto
		{
			ScaleMin = 0,
			ScaleMax = 100,
			Plots = [new() { Kind = IndicatorPlotKind.Line, Values = [40, 60] }],
		};
		var free = new IndicatorResultDto
		{
			Plots = [new() { Kind = IndicatorPlotKind.Line, Values = [40, 60] }],
		};

		Assert.Equal((0d, 100d), IndicatorChartHelpers.GetPaneScale(rsi, 0, 2));
		var (min, max) = IndicatorChartHelpers.GetPaneScale(free, 0, 2);
		Assert.Equal((38.4, 61.6), (Math.Round(min, 6), Math.Round(max, 6)));
		var (emptyMin, emptyMax) = IndicatorChartHelpers.GetPaneScale(
			new IndicatorResultDto(),
			0,
			0
		);
		Assert.Equal((-0.08, 1.08), (Math.Round(emptyMin, 6), Math.Round(emptyMax, 6)));
	}

	[Fact]
	public void User_colours_replace_the_plot_colours_by_name()
	{
		var plots = new List<IndicatorPlotDto>
		{
			new() { Name = "Fast", Color = 1 },
			new() { Name = "Slow", Color = 2 },
		};

		IndicatorChartHelpers.ApplyPlotColors(plots, new Dictionary<string, uint> { ["Slow"] = 9 });

		Assert.Equal([1u, 9u], plots.Select(x => x.Color));
	}

	[Fact]
	public void The_legend_names_the_values_only_when_an_indicator_shows_more_than_one()
	{
		var rvol = IndicatorChartHelpers.GetLegendPlots([
			new() { Name = "Today", Kind = IndicatorPlotKind.Line },
			new() { Name = "Average", Kind = IndicatorPlotKind.Line },
			new() { Name = "Signal", Kind = IndicatorPlotKind.Markers },
			new() { Name = "Level", Kind = IndicatorPlotKind.Level },
		]);
		var single = IndicatorChartHelpers.GetLegendPlots([
			new() { Name = "RSI", Kind = IndicatorPlotKind.Line },
			new() { Name = "70", Kind = IndicatorPlotKind.Level },
		]);

		Assert.Equal(["Today", "Average"], rvol.Select(x => x.Name));
		Assert.Equal(["Today", "Average"], rvol.Select(x => x.Plot.Name));
		Assert.Equal("RSI", Assert.Single(single).Plot.Name);
		Assert.Null(single[0].Name);
	}
}
