using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Settings;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

/// <summary>Layout files: the whole layout exported to JSON and imported back.</summary>
public class SettingsManagerLayoutFileTests
{
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly InMemorySettingsRepository _settings = new();
	private readonly SettingsManager _manager;

	public SettingsManagerLayoutFileTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new SettingsManager(_settings, _ibkr);
	}

	private static LayoutEntity CreateLayout() =>
		new()
		{
			WindowWidth = 1600,
			WindowHeight = 1000,
			WindowX = 120,
			WindowY = 80,
			WindowState = WindowPlacementState.Normal,
			WindowScreen = new WindowScreenEntity
			{
				Name = "DP-1",
				Width = 2560,
				Height = 1440,
			},
			ChartTimeframe = ChartTimeframe.OneHour,
			LeftWidth = 300,
			LeftWidgetId = "watchlist",
			Widgets =
			[
				new WidgetPlacementEntity
				{
					Id = "watchlist",
					Tool = WorkspaceTool.Watchlist,
					Dock = WidgetDock.Left,
					WatchlistId = "tech",
				},
				new WidgetPlacementEntity
				{
					Id = "chart-1",
					Tool = WorkspaceTool.Chart,
					Dock = WidgetDock.Center,
					ChartTimeframe = ChartTimeframe.OneDay,
					ChartPeriod = ChartPeriod.Auto,
					LinkGroup = LinkGroup.Blue,
					ChartContract = new ContractEntity
					{
						ConId = 265598,
						Symbol = "AAPL",
						SecurityType = "STK",
						Currency = "USD",
					},
					ChartIndicators =
					[
						new IndicatorInstanceEntity
						{
							Id = "ema",
							Inputs = new Dictionary<string, double> { ["Length"] = 21 },
						},
					],
				},
			],
			Views =
			[
				new CenterViewEntity
				{
					Id = "main",
					Name = "Main",
					Root = new CenterNodeEntity { WidgetId = "chart-1" },
				},
			],
			SelectedViewId = "main",
			Windows =
			[
				new WorkspaceWindowEntity
				{
					Id = "w2",
					Name = "Window 2",
					X = 2600,
					Y = 40,
					Width = 900,
					Height = 700,
					State = WindowPlacementState.Maximized,
					Widgets =
					[
						new WidgetPlacementEntity
						{
							Id = "news",
							Tool = WorkspaceTool.News,
							Dock = WidgetDock.Floating,
							FloatX = 10,
							FloatY = 20,
							FloatWidth = 400,
							FloatHeight = 300,
						},
					],
				},
			],
		};

	[Fact]
	public void Exported_layout_imports_into_another_install_unchanged()
	{
		_settings.Settings.Layout = CreateLayout();
		var json = _manager.ExportLayout();
		var otherSettings = new InMemorySettingsRepository();
		var other = new SettingsManager(otherSettings, _ibkr);

		var imported = other.ImportLayout(new ImportLayoutRequest { Json = json });

		Assert.Equal(
			LayoutFileHelpers.SerializeFile(CreateLayout()),
			LayoutFileHelpers.SerializeFile(otherSettings.Settings.Layout)
		);
		Assert.Equal(
			(1600, 1000, 120d),
			(imported.WindowWidth, imported.WindowHeight, imported.WindowX!.Value)
		);
		Assert.Equal(["watchlist", "chart-1"], imported.Widgets.Select(x => x.Id));
		var chart = imported.Widgets[1];
		Assert.Equal(
			(ChartTimeframe.OneDay, LinkGroup.Blue, "AAPL", "ema"),
			(
				chart.ChartTimeframe,
				chart.LinkGroup,
				chart.ChartContract!.Symbol,
				chart.ChartIndicators![0].Id
			)
		);
		Assert.Equal("chart-1", imported.Views.Single().Root!.WidgetId);
		var window = Assert.Single(imported.Windows);
		Assert.Equal(
			("Window 2", WindowPlacementState.Maximized, WidgetDock.Floating, 400d),
			(window.Name, window.State, window.Widgets[0].Dock, window.Widgets[0].FloatWidth!.Value)
		);
	}

	[Fact]
	public void Export_holds_only_the_layout()
	{
		_settings.Settings.Flex.Token = "secret-flex-token";
		_settings.Settings.Connection.Host = "10.1.2.3";
		_settings.Settings.SelectedAccount = "U1234567";

		var json = _manager.ExportLayout();

		Assert.Contains(LayoutFileConstants.FileFormat, json);
		Assert.DoesNotContain("secret-flex-token", json);
		Assert.DoesNotContain("10.1.2.3", json);
		Assert.DoesNotContain("U1234567", json);
	}

	[Fact]
	public void Import_keeps_everything_but_the_layout()
	{
		var source = new InMemorySettingsRepository();
		source.Settings.Layout = CreateLayout();
		source.Settings.Flex.Token = "theirs";
		var json = new SettingsManager(source, _ibkr).ExportLayout();
		_settings.Settings.Flex.Token = "mine";
		_settings.Settings.SelectedAccount = "U1";

		_manager.ImportLayout(new ImportLayoutRequest { Json = json });

		Assert.Equal(
			("mine", "U1"),
			(_settings.Settings.Flex.Token, _settings.Settings.SelectedAccount)
		);
		Assert.Equal("w2", Assert.Single(_settings.Settings.Layout.Windows).Id);
	}

	[Fact]
	public void A_file_from_a_newer_version_is_refused_and_the_layout_kept()
	{
		_settings.Settings.Layout = CreateLayout();
		var before = _manager.ExportLayout();
		var json = before.Replace(
			$"\"version\": {LayoutFileConstants.CurrentVersion}",
			$"\"version\": {LayoutFileConstants.CurrentVersion + 1}"
		);
		Assert.NotEqual(before, json);

		var error = Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.ImportLayout(new ImportLayoutRequest { Json = json })
		);

		Assert.Contains("newer version", error.Message);
		Assert.Equal(before, _manager.ExportLayout());
	}

	[Theory]
	[InlineData("")]
	[InlineData("{ \"format\": \"LimitlessTrading.Layout\", \"version\": 1, \"layout\": { ")]
	[InlineData("not json at all")]
	[InlineData("null")]
	[InlineData("{ \"format\": \"LimitlessTrading.Layout\", \"version\": 1 }")]
	[InlineData("{ \"format\": \"LimitlessTrading.Layout\", \"version\": 0, \"layout\": {} }")]
	[InlineData(
		"{ \"format\": \"LimitlessTrading.Layout\", \"version\": 1, \"layout\": { \"widgets\": 5 } }"
	)]
	public void A_damaged_file_is_refused_and_the_layout_kept(string json)
	{
		_settings.Settings.Layout = CreateLayout();
		var before = _manager.ExportLayout();

		var error = Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.ImportLayout(new ImportLayoutRequest { Json = json })
		);

		Assert.Contains("damaged", error.Message);
		Assert.Equal(before, _manager.ExportLayout());
	}

	[Fact]
	public void A_file_of_another_kind_is_refused()
	{
		var json = """
			{ "format": "LimitlessTrading.ChartTemplates", "version": 2, "templates": [] }
			""";

		var error = Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.ImportLayout(new ImportLayoutRequest { Json = json })
		);

		Assert.Contains("not a LimitlessTrading layout file", error.Message);
	}

	[Fact]
	public void A_file_over_the_size_limit_is_refused()
	{
		var json = new string(' ', LayoutFileConstants.MaxFileLength + 1);

		var error = Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.ImportLayout(new ImportLayoutRequest { Json = json })
		);

		Assert.Contains("larger than", error.Message);
	}

	[Fact]
	public void An_older_file_without_new_fields_imports_with_their_defaults()
	{
		var json = """
			{
			  // hand-edited
			  "format": "LimitlessTrading.Layout",
			  "version": 1,
			  "layout": {
			    "widgets": [ { "id": "chart", "tool": 6, "dock": 0 } ],
			  }
			}
			""";

		var layout = _manager.ImportLayout(new ImportLayoutRequest { Json = json });

		Assert.Equal("chart", Assert.Single(layout.Widgets).Id);
		Assert.Empty(layout.Windows);
		Assert.Equal(WorkspaceConstants.DefaultLeftWidth, layout.LeftWidth);
	}
}
