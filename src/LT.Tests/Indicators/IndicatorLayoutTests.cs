using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Indicators;

public class IndicatorLayoutTests
{
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly InMemorySettingsRepository _settings = new();

	public IndicatorLayoutTests() => LSCoreContainer.EnsureInitialized();

	[Fact]
	public void A_charts_indicators_round_trip_with_the_layout()
	{
		var manager = new SettingsManager(_settings, _ibkr);
		var layout = manager.GetLayout();
		layout.Widgets =
		[
			new WidgetPlacementDto
			{
				Id = "chart-1",
				Tool = WorkspaceTool.Chart,
				ChartIndicators =
				[
					new IndicatorInstanceDto
					{
						Id = "a",
						DefinitionId = "builtin:rsi",
						Inputs = new() { ["Length"] = 7 },
					},
					new IndicatorInstanceDto
					{
						Id = "b",
						DefinitionId = "script:ema-cross.cs",
						IsVisible = false,
						PlotColors = new() { ["Fast"] = 0xFF00FF00 },
					},
				],
			},
		];

		manager.SaveLayout(layout);

		var saved = Assert.Single(manager.GetLayout().Widgets).ChartIndicators!;
		Assert.Equal(["a", "b"], saved.Select(x => x.Id));
		Assert.Equal(
			("builtin:rsi", 7d, true),
			(saved[0].DefinitionId, saved[0].Inputs["Length"], saved[0].IsVisible)
		);
		Assert.False(saved[1].IsVisible);
		Assert.Equal(0xFF00FF00, saved[1].PlotColors!["Fast"]);
	}

	[Fact]
	public void The_volume_migration_mark_round_trips_with_the_layout()
	{
		var manager = new SettingsManager(_settings, _ibkr);
		var layout = manager.GetLayout();
		layout.Widgets =
		[
			new WidgetPlacementDto
			{
				Id = "chart-1",
				Tool = WorkspaceTool.Chart,
				ChartVolumeMigrated = true,
			},
		];

		manager.SaveLayout(layout);

		Assert.True(Assert.Single(manager.GetLayout().Widgets).ChartVolumeMigrated);
		Assert.Null(
			System
				.Text.Json.JsonSerializer.Deserialize<WidgetPlacementEntity>(
					"""{"Id":"chart-1","Tool":6}"""
				)!
				.ChartVolumeMigrated
		);
	}

	[Fact]
	public void A_layout_saved_before_indicators_loads_with_none()
	{
		_settings.Settings.Layout.Widgets =
		[
			new WidgetPlacementEntity { Id = "chart-1", Tool = WorkspaceTool.Chart },
		];

		var placement = Assert.Single(new SettingsManager(_settings, _ibkr).GetLayout().Widgets);

		Assert.Null(placement.ChartIndicators);
	}

	[Fact]
	public void Old_settings_json_without_the_field_deserializes()
	{
		var entity = System.Text.Json.JsonSerializer.Deserialize<WidgetPlacementEntity>(
			"""{"Id":"chart-1","Tool":6,"ChartRightMargin":3}"""
		)!;

		Assert.Null(entity.ChartIndicators);
		Assert.Equal(3, entity.ChartRightMargin);
	}
}
