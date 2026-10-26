using System.Text.Json;
using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.ChartTemplates;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Requests.ChartTemplates;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class ChartTemplateManagerTests
{
	private readonly InMemorySettingsRepository _settings = new();
	private readonly ChartTemplateManager _manager;

	public ChartTemplateManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new ChartTemplateManager(_settings);
	}

	[Fact]
	public void Saved_template_comes_back_with_every_setting()
	{
		_manager.Save(new SaveChartTemplateRequest { Name = " Scalping ", Settings = Scalping() });

		var template = Assert.Single(_manager.GetTemplates());
		Assert.Equal("Scalping", template.Name);
		Assert.Equal(ChartTemplateConstants.CurrentVersion, template.Version);
		AssertScalping(template.Settings);
	}

	[Fact]
	public void Saved_template_survives_the_settings_file()
	{
		_manager.Save(new SaveChartTemplateRequest { Name = "Scalping", Settings = Scalping() });

		// The way SettingsRepository writes and reads settings.json
		var json = JsonSerializer.Serialize(_settings.Settings);
		_settings.Save(JsonSerializer.Deserialize<SettingsEntity>(json)!);

		AssertScalping(Assert.Single(_manager.GetTemplates()).Settings);
	}

	[Fact]
	public void Settings_saved_before_templates_have_none()
	{
		_settings.Save(JsonSerializer.Deserialize<SettingsEntity>("{\"SelectedAccount\":\"U1\"}")!);

		Assert.Empty(_manager.GetTemplates());
		Assert.Null(_manager.GetDefault());
	}

	[Fact]
	public void A_taken_name_is_refused_without_overwrite()
	{
		_manager.Save(new SaveChartTemplateRequest { Name = "Swing", Settings = Scalping() });

		var ex = Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.Save(
				new SaveChartTemplateRequest
				{
					Name = "swing",
					Settings = new ChartTemplateSettingsDto { Timeframe = ChartTimeframe.OneDay },
				}
			)
		);
		Assert.Contains("Swing", ex.Message);
	}

	[Fact]
	public void Overwrite_replaces_the_settings_and_keeps_the_id_and_default()
	{
		var saved = _manager.Save(
			new SaveChartTemplateRequest { Name = "Swing", Settings = Scalping() }
		);
		_manager.SetDefault(saved.Id);

		_manager.Save(
			new SaveChartTemplateRequest
			{
				Name = "swing",
				Settings = new ChartTemplateSettingsDto
				{
					Timeframe = ChartTimeframe.OneDay,
					Indicators = [],
				},
				Overwrite = true,
			}
		);

		var template = Assert.Single(_manager.GetTemplates());
		Assert.Equal(saved.Id, template.Id);
		Assert.True(template.IsDefault);
		Assert.Equal(ChartTimeframe.OneDay, template.Settings.Timeframe);
		Assert.Empty(template.Settings.Indicators!);
	}

	[Fact]
	public void Updating_settings_keeps_the_name_and_default_and_tells_listeners()
	{
		var saved = _manager.Save(
			new SaveChartTemplateRequest { Name = "Swing", Settings = Scalping() }
		);
		_manager.SetDefault(saved.Id);
		var heard = new List<ChartTemplateDto>();
		_manager.TemplateSaved += heard.Add;

		_manager.UpdateSettings(
			new UpdateChartTemplateSettingsRequest
			{
				Id = saved.Id,
				Settings = new ChartTemplateSettingsDto { Timeframe = ChartTimeframe.OneDay },
			}
		);

		var template = Assert.Single(_manager.GetTemplates());
		Assert.Equal("Swing", template.Name);
		Assert.True(template.IsDefault);
		Assert.Equal(ChartTimeframe.OneDay, template.Settings.Timeframe);
		Assert.Equal(ChartTimeframe.OneDay, Assert.Single(heard).Settings.Timeframe);
	}

	[Fact]
	public void Updating_a_deleted_template_is_refused() =>
		Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.UpdateSettings(
				new UpdateChartTemplateSettingsRequest
				{
					Id = "gone",
					Settings = new ChartTemplateSettingsDto(),
				}
			)
		);

	[Fact]
	public void Saving_tells_listeners()
	{
		var heard = new List<ChartTemplateDto>();
		_manager.TemplateSaved += heard.Add;

		_manager.Save(new SaveChartTemplateRequest { Name = "Swing", Settings = Scalping() });

		Assert.Equal("Swing", Assert.Single(heard).Name);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void A_blank_name_is_refused(string name) =>
		Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.Save(new SaveChartTemplateRequest { Name = name, Settings = Scalping() })
		);

	[Fact]
	public void A_too_long_name_is_refused() =>
		Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.Save(
				new SaveChartTemplateRequest
				{
					Name = new string('x', ChartTemplateConstants.NameMaxLength + 1),
					Settings = Scalping(),
				}
			)
		);

	[Fact]
	public void Rename_refuses_a_name_another_template_has()
	{
		var first = _manager.Save(new SaveChartTemplateRequest { Name = "A", Settings = new() });
		_manager.Save(new SaveChartTemplateRequest { Name = "B", Settings = new() });

		Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.Rename(new RenameChartTemplateRequest { Id = first.Id, Name = "b" })
		);
		_manager.Rename(new RenameChartTemplateRequest { Id = first.Id, Name = "a" });

		Assert.Equal(["a", "B"], _manager.GetTemplates().Select(x => x.Name));
	}

	[Fact]
	public void Deleting_the_default_template_unsets_the_default()
	{
		var saved = _manager.Save(new SaveChartTemplateRequest { Name = "A", Settings = new() });
		_manager.SetDefault(saved.Id);

		_manager.Delete(saved.Id);

		Assert.Empty(_manager.GetTemplates());
		Assert.Null(_manager.GetDefault());
		Assert.Null(_settings.Settings.DefaultChartTemplateId);
	}

	[Fact]
	public void Export_then_import_gives_the_same_templates()
	{
		_manager.Save(new SaveChartTemplateRequest { Name = "Scalping", Settings = Scalping() });
		_manager.Save(
			new SaveChartTemplateRequest
			{
				Name = "Daily",
				Settings = new ChartTemplateSettingsDto { Timeframe = ChartTimeframe.OneDay },
			}
		);
		var json = _manager.Export();

		var other = new ChartTemplateManager(new InMemorySettingsRepository());
		var imported = other.Import(json);

		Assert.Equal(["Daily", "Scalping"], imported.Select(x => x.Name));
		AssertScalping(other.FindByName("Scalping")!.Settings);
		var daily = other.FindByName("Daily")!.Settings;
		Assert.Equal(ChartTimeframe.OneDay, daily.Timeframe);
		Assert.Null(daily.Indicators);
		Assert.Null(daily.RightMargin);
	}

	[Fact]
	public void Export_of_one_template_holds_only_that_one()
	{
		var scalping = _manager.Save(
			new SaveChartTemplateRequest { Name = "Scalping", Settings = Scalping() }
		);
		_manager.Save(new SaveChartTemplateRequest { Name = "Daily", Settings = new() });

		var imported = new ChartTemplateManager(new InMemorySettingsRepository()).Import(
			_manager.Export([scalping.Id])
		);

		Assert.Equal("Scalping", Assert.Single(imported).Name);
	}

	[Fact]
	public void Imported_names_already_taken_get_a_number()
	{
		_manager.Save(new SaveChartTemplateRequest { Name = "Scalping", Settings = Scalping() });
		var json = _manager.Export();

		_manager.Import(json);
		var third = _manager.Import(json);

		Assert.Equal("Scalping (3)", Assert.Single(third).Name);
		Assert.Equal(
			["Scalping", "Scalping (2)", "Scalping (3)"],
			_manager.GetTemplates().Select(x => x.Name)
		);
		Assert.Equal(3, _manager.GetTemplates().Select(x => x.Id).Distinct().Count());
	}

	[Fact]
	public void Imported_file_with_two_templates_of_one_name_keeps_both()
	{
		var json = """
			{
			  "format": "LimitlessTrading.ChartTemplates",
			  "version": 1,
			  "templates": [
			    { "name": "Mine", "settings": { "timeframe": "OneMinute" } },
			    { "name": "mine", "settings": { "timeframe": "OneHour" } }
			  ]
			}
			""";

		var imported = _manager.Import(json);

		Assert.Equal(["Mine", "mine (2)"], imported.Select(x => x.Name));
	}

	[Fact]
	public void Import_reads_a_newer_file_and_ignores_what_it_does_not_know()
	{
		// A later version with settings and fields this one doesn't have
		var json = """
			{
			  "format": "LimitlessTrading.ChartTemplates",
			  "version": 7,
			  "exportedBy": "LimitlessTrading 9.0",
			  "templates": [
			    {
			      "name": "Future",
			      "version": 7,
			      "colorTheme": "dark",
			      "settings": {
			        "timeframe": "FifteenMinutes",
			        "drawings": [ { "kind": "trendline", "points": [1, 2] } ],
			        "indicators": [
			          {
			            "definitionId": "builtin:ema",
			            "inputs": { "Length": 50 },
			            "lineWidth": 3
			          }
			        ]
			      }
			    }
			  ]
			}
			""";

		var template = Assert.Single(_manager.Import(json));

		Assert.Equal("Future", template.Name);
		Assert.Equal(ChartTemplateConstants.CurrentVersion, template.Version);
		Assert.Equal(ChartTimeframe.FifteenMinutes, template.Settings.Timeframe);
		var indicator = Assert.Single(template.Settings.Indicators!);
		Assert.Equal("builtin:ema", indicator.DefinitionId);
		Assert.Equal(50, indicator.Inputs["Length"]);
		Assert.True(indicator.IsVisible);
	}

	[Fact]
	public void A_template_saved_before_periods_is_followed_with_its_period()
	{
		// settings.json written by version 1: the timeframe meant its own span (Auto)
		_settings.Save(
			JsonSerializer.Deserialize<SettingsEntity>(
				"""
				{ "ChartTemplates": [ { "Id": "old", "Name": "5min", "Version": 1,
				  "Settings": { "Timeframe": 1, "RightMargin": 5 } } ] }
				"""
			)!
		);
		var template = _manager.GetTemplate("old")!;
		Assert.Equal(ChartPeriod.Auto, template.Settings.Period);

		// A follower's period edit is kept; the template is written as the current version
		var settings = template.Settings;
		settings.Period = ChartPeriod.OneMonth;
		_manager.UpdateSettings(new() { Id = "old", Settings = settings });

		var updated = _manager.GetTemplate("old")!;
		Assert.Equal(ChartPeriod.OneMonth, updated.Settings.Period);
		Assert.Equal(ChartTemplateConstants.CurrentVersion, updated.Version);
	}

	[Fact]
	public void Import_takes_timeframes_as_numbers_and_any_property_case()
	{
		var json = """
			{ "Format": "LimitlessTrading.ChartTemplates", "Templates": [
			  { "Name": "Old", "Settings": { "Timeframe": 3, "RightMargin": 5 } }, ] }
			""";

		var template = Assert.Single(_manager.Import(json));

		Assert.Equal(ChartTimeframe.OneHour, template.Settings.Timeframe);
		Assert.Equal(5, template.Settings.RightMargin);
	}

	[Theory]
	[InlineData("not json at all")]
	[InlineData("[1, 2, 3]")]
	[InlineData("{ \"templates\": [ { \"name\": \"No format\" } ] }")]
	[InlineData("{ \"format\": \"Something.Else\", \"templates\": [ { \"name\": \"A\" } ] }")]
	[InlineData("{ \"format\": \"LimitlessTrading.ChartTemplates\", \"templates\": [] }")]
	[InlineData("{ \"format\": \"LimitlessTrading.ChartTemplates\" }")]
	[InlineData(
		"{ \"format\": \"LimitlessTrading.ChartTemplates\", \"templates\": [ { \"name\": \" \" } ] }"
	)]
	[InlineData(
		"{ \"format\": \"LimitlessTrading.ChartTemplates\", \"templates\": [ { \"name\": \"A\", \"settings\": { \"timeframe\": \"TwoMinutes\" } } ] }"
	)]
	[InlineData(
		"{ \"format\": \"LimitlessTrading.ChartTemplates\", \"templates\": [ { \"name\": \"A\", \"settings\": { \"timeframe\": 42 } } ] }"
	)]
	[InlineData(
		"{ \"format\": \"LimitlessTrading.ChartTemplates\", \"templates\": [ { \"name\": \"A\", \"settings\": { \"rightMargin\": -1 } } ] }"
	)]
	[InlineData(
		"{ \"format\": \"LimitlessTrading.ChartTemplates\", \"templates\": [ { \"name\": \"A\", \"settings\": { \"period\": 42 } } ] }"
	)]
	[InlineData(
		"{ \"format\": \"LimitlessTrading.ChartTemplates\", \"templates\": [ { \"name\": \"A\", \"settings\": { \"indicators\": [ { \"inputs\": {} } ] } } ] }"
	)]
	[InlineData(
		"{ \"format\": \"LimitlessTrading.ChartTemplates\", \"templates\": [ { \"name\": \"A\", \"settings\": { \"indicators\": [ { \"definitionId\": \"builtin:sma\", \"inputs\": { \"Length\": \"long\" } } ] } } ] }"
	)]
	public void An_invalid_file_is_refused_and_nothing_is_added(string json)
	{
		_manager.Save(new SaveChartTemplateRequest { Name = "Kept", Settings = new() });

		Assert.Throws<LSCoreBadRequestException>(() => _manager.Import(json));

		Assert.Equal("Kept", Assert.Single(_manager.GetTemplates()).Name);
	}

	[Fact]
	public void One_invalid_template_refuses_the_whole_file_and_names_it()
	{
		var json = """
			{ "format": "LimitlessTrading.ChartTemplates", "templates": [
			  { "name": "Good", "settings": { "timeframe": "OneDay" } },
			  { "name": "Bad", "settings": { "rightMargin": -3 } } ] }
			""";

		var ex = Assert.Throws<LSCoreBadRequestException>(() => _manager.Import(json));

		Assert.Contains("Bad", ex.Message);
		Assert.Empty(_manager.GetTemplates());
	}

	[Fact]
	public void A_file_with_too_many_indicators_in_a_template_is_refused()
	{
		var file = new ChartTemplateFileEntity
		{
			Format = ChartTemplateConstants.FileFormat,
			Templates =
			[
				new ChartTemplateEntity
				{
					Name = "Crowded",
					Settings = new ChartTemplateSettingsEntity
					{
						Indicators =
						[
							.. Enumerable
								.Range(0, IndicatorConstants.MaxIndicatorsPerChart + 1)
								.Select(_ => new IndicatorInstanceEntity
								{
									DefinitionId = "builtin:sma",
								}),
						],
					},
				},
			],
		};

		Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.Import(JsonSerializer.Serialize(file))
		);
	}

	[Fact]
	public void A_file_over_the_size_limit_is_refused() =>
		Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.Import(new string(' ', ChartTemplateConstants.MaxFileLength + 1))
		);

	private static ChartTemplateSettingsDto Scalping() =>
		new()
		{
			Timeframe = ChartTimeframe.OneMinute,
			Period = ChartPeriod.Today,
			RightMargin = 12,
			Indicators =
			[
				new IndicatorInstanceDto
				{
					DefinitionId = "builtin:ema",
					Inputs = new Dictionary<string, double> { ["Length"] = 9 },
				},
				new IndicatorInstanceDto
				{
					DefinitionId = "script:ema-cross.cs",
					Inputs = new Dictionary<string, double> { ["Fast"] = 5, ["Slow"] = 13 },
					IsVisible = false,
					PlotColors = new Dictionary<string, uint> { ["Fast"] = 0xFF00FF00 },
				},
			],
		};

	private static void AssertScalping(ChartTemplateSettingsDto settings)
	{
		Assert.Equal(ChartTimeframe.OneMinute, settings.Timeframe);
		Assert.Equal(ChartPeriod.Today, settings.Period);
		Assert.Equal(12, settings.RightMargin);
		Assert.Collection(
			settings.Indicators!,
			ema =>
			{
				Assert.Equal("builtin:ema", ema.DefinitionId);
				Assert.Equal(9, ema.Inputs["Length"]);
				Assert.True(ema.IsVisible);
				Assert.Null(ema.PlotColors);
			},
			cross =>
			{
				Assert.Equal("script:ema-cross.cs", cross.DefinitionId);
				Assert.Equal(13, cross.Inputs["Slow"]);
				Assert.False(cross.IsVisible);
				Assert.Equal(0xFF00FF00u, cross.PlotColors!["Fast"]);
			}
		);
	}
}
