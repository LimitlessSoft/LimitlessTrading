using LT.Contracts.Constants;
using LT.Contracts.Dtos.ChartTemplates;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class ChartTemplateHelpersTests
{
	[Fact]
	public void A_free_name_is_kept_as_it_is() =>
		Assert.Equal("Swing", ChartTemplateHelpers.GetFreeName(" Swing ", ["Scalping"]));

	[Fact]
	public void A_taken_name_gets_the_first_free_number() =>
		Assert.Equal(
			"Swing (3)",
			ChartTemplateHelpers.GetFreeName("Swing", ["swing", "Swing (2)", "Swing (4)"])
		);

	[Fact]
	public void A_numbered_name_stays_within_the_length_limit()
	{
		var name = new string('x', ChartTemplateConstants.NameMaxLength);

		var free = ChartTemplateHelpers.GetFreeName(name, [name]);

		Assert.Equal(ChartTemplateConstants.NameMaxLength, free.Length);
		Assert.EndsWith(" (2)", free);
	}

	[Theory]
	[InlineData("Scalping", "Scalping.json")]
	[InlineData("1m / 5m: EMA", "1m _ 5m_ EMA.json")]
	[InlineData("   ", "chart-templates.json")]
	public void File_names_drop_characters_file_systems_refuse(string name, string expected) =>
		Assert.Equal(expected, ChartTemplateHelpers.GetFileName(name));

	[Fact]
	public void A_chart_matches_a_template_by_indicator_content_not_instance()
	{
		var template = new ChartTemplateSettingsDto
		{
			Timeframe = ChartTimeframe.OneHour,
			Indicators = [Ema(20)],
		};

		Assert.True(ChartTemplateHelpers.Matches(template, Chart(ChartTimeframe.OneHour, Ema(20))));
		Assert.False(
			ChartTemplateHelpers.Matches(template, Chart(ChartTimeframe.OneHour, Ema(50)))
		);
		Assert.False(ChartTemplateHelpers.Matches(template, Chart(ChartTimeframe.OneDay, Ema(20))));
	}

	[Fact]
	public void Settings_a_template_does_not_hold_never_make_a_chart_differ() =>
		Assert.True(
			ChartTemplateHelpers.Matches(
				new ChartTemplateSettingsDto { Timeframe = ChartTimeframe.OneHour },
				Chart(ChartTimeframe.OneHour, Ema(20))
			)
		);

	[Fact]
	public void Merging_keeps_the_templates_shape()
	{
		var merged = ChartTemplateHelpers.Merge(
			new ChartTemplateSettingsDto { Timeframe = ChartTimeframe.OneHour },
			Chart(ChartTimeframe.OneDay, Ema(20))
		);

		Assert.Equal(ChartTimeframe.OneDay, merged.Timeframe);
		Assert.Null(merged.RightMargin);
		Assert.Null(merged.Indicators);
	}

	[Fact]
	public void A_template_holding_a_period_differs_from_a_chart_at_another_one()
	{
		var template = new ChartTemplateSettingsDto { Period = ChartPeriod.OneMonth };
		var chart = Chart(ChartTimeframe.OneHour);

		chart.Period = ChartPeriod.OneMonth;
		Assert.True(ChartTemplateHelpers.Matches(template, chart));
		chart.Period = ChartPeriod.Auto;
		Assert.False(ChartTemplateHelpers.Matches(template, chart));
	}

	[Fact]
	public void Merging_takes_the_charts_period_only_when_the_template_holds_one()
	{
		var chart = Chart(ChartTimeframe.OneHour);
		chart.Period = ChartPeriod.ThisWeek;

		Assert.Equal(
			ChartPeriod.ThisWeek,
			ChartTemplateHelpers.Merge(new() { Period = ChartPeriod.Auto }, chart).Period
		);
		Assert.Null(
			ChartTemplateHelpers.Merge(new() { Timeframe = ChartTimeframe.OneDay }, chart).Period
		);
	}

	[Fact]
	public void A_template_from_before_periods_holds_auto_with_its_timeframe()
	{
		var old = new ChartTemplateEntity
		{
			Id = "a",
			Name = "Old",
			Version = 1,
			Settings = new() { Timeframe = ChartTimeframe.FiveMinutes },
		};
		var empty = new ChartTemplateEntity
		{
			Id = "b",
			Name = "Indicators only",
			Version = 1,
			Settings = new(),
		};
		var current = ChartTemplateHelpers.ToEntity(
			"c",
			"New",
			new() { Timeframe = ChartTimeframe.FiveMinutes }
		);

		Assert.Equal(ChartPeriod.Auto, ChartTemplateHelpers.ToDto(old, null).Settings.Period);
		Assert.Null(ChartTemplateHelpers.ToDto(empty, null).Settings.Period);
		// Written since: a template without a period doesn't hold one
		Assert.Null(ChartTemplateHelpers.ToDto(current, null).Settings.Period);
	}

	[Fact]
	public void An_unknown_period_is_refused() =>
		Assert.Equal(
			"Mine: unknown period.",
			ChartTemplateHelpers.GetSettingsError("Mine", new() { Period = (ChartPeriod)42 })
		);

	private static IndicatorInstanceDto Ema(double length) =>
		new()
		{
			DefinitionId = "ema",
			Inputs = new() { ["Length"] = length },
		};

	private static ChartTemplateSettingsDto Chart(
		ChartTimeframe timeframe,
		params IndicatorInstanceDto[] indicators
	) =>
		new()
		{
			Timeframe = timeframe,
			RightMargin = 0,
			Indicators = [.. indicators],
		};
}
