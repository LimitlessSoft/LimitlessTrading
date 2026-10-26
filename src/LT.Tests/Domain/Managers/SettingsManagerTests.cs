using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Settings;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class SettingsManagerTests
{
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly InMemorySettingsRepository _settings = new();

	public SettingsManagerTests() => LSCoreContainer.EnsureInitialized();

	[Fact]
	public void Save_persists_raises_and_applies_market_data_type()
	{
		var manager = new SettingsManager(_settings, _ibkr);
		var raised = false;
		manager.AppSettingsChanged += _ => raised = true;

		manager.SaveAppSettings(
			new SaveAppSettingsRequest
			{
				MarketDataType = MarketDataType.Live,
				AutoReconnect = false,
				HeartbeatSeconds = 10,
				StopGatewayOnExit = true,
			}
		);

		Assert.True(raised);
		Assert.Equal(MarketDataType.Live, _ibkr.MarketDataType);
		var saved = manager.GetAppSettings();
		Assert.Equal(
			(MarketDataType.Live, false, 10, true),
			(
				saved.MarketDataType,
				saved.AutoReconnect,
				saved.HeartbeatSeconds,
				saved.StopGatewayOnExit
			)
		);
	}

	[Fact]
	public void Gateway_keeps_running_on_exit_by_default() =>
		Assert.False(new SettingsManager(_settings, _ibkr).GetAppSettings().StopGatewayOnExit);

	[Fact]
	public void Cache_is_on_with_a_500_mb_limit_by_default()
	{
		var settings = new SettingsManager(_settings, _ibkr).GetAppSettings();

		Assert.True(settings.CacheEnabled);
		Assert.True(settings.CacheChartBars);
		Assert.True(settings.CacheContracts);
		Assert.True(settings.CacheOptionChains);
		Assert.True(settings.CacheExecutions);
		Assert.True(settings.CacheSnapshots);
		Assert.Equal(CacheConstants.DefaultMaxSizeMb, settings.CacheMaxSizeMb);
	}

	[Fact]
	public void Cache_settings_round_trip()
	{
		var manager = new SettingsManager(_settings, _ibkr);

		manager.SaveAppSettings(
			new SaveAppSettingsRequest
			{
				MarketDataType = MarketDataType.Delayed,
				HeartbeatSeconds = 10,
				CacheEnabled = false,
				CacheExecutions = false,
				CacheMaxSizeMb = 50,
			}
		);

		var settings = manager.GetAppSettings();
		Assert.False(settings.CacheEnabled);
		Assert.False(settings.CacheExecutions);
		Assert.True(settings.CacheSnapshots);
		Assert.Equal(50, settings.CacheMaxSizeMb);
	}

	[Theory]
	[InlineData(CacheConstants.MaxSizeMbMin - 1)]
	[InlineData(CacheConstants.MaxSizeMbMax + 1)]
	public void Cache_size_outside_the_limits_is_rejected(int sizeMb) =>
		Assert.Throws<LSCoreBadRequestException>(() =>
			new SettingsManager(_settings, _ibkr).SaveAppSettings(
				new SaveAppSettingsRequest
				{
					MarketDataType = MarketDataType.Delayed,
					HeartbeatSeconds = 10,
					CacheMaxSizeMb = sizeMb,
				}
			)
		);

	[Fact]
	public void Display_time_zone_is_local_by_default_and_persisted_when_picked()
	{
		var manager = new SettingsManager(_settings, _ibkr);
		Assert.Null(manager.GetAppSettings().DisplayTimeZoneId);

		manager.SaveAppSettings(
			new SaveAppSettingsRequest
			{
				MarketDataType = MarketDataType.Delayed,
				HeartbeatSeconds = 10,
				DisplayTimeZoneId = DisplayTimeConstants.ExchangeTimeZoneId,
			}
		);

		Assert.Equal(
			DisplayTimeConstants.ExchangeTimeZoneId,
			manager.GetAppSettings().DisplayTimeZoneId
		);
	}

	[Fact]
	public void Unknown_display_time_zone_is_rejected() =>
		Assert.Throws<LSCoreBadRequestException>(() =>
			new SettingsManager(_settings, _ibkr).SaveAppSettings(
				new SaveAppSettingsRequest
				{
					MarketDataType = MarketDataType.Delayed,
					HeartbeatSeconds = 10,
					DisplayTimeZoneId = "Not/AZone",
				}
			)
		);

	[Fact]
	public void Invalid_heartbeat_is_rejected() =>
		Assert.Throws<LSCoreBadRequestException>(() =>
			new SettingsManager(_settings, _ibkr).SaveAppSettings(
				new SaveAppSettingsRequest
				{
					MarketDataType = MarketDataType.Delayed,
					HeartbeatSeconds = 1,
				}
			)
		);

	[Fact]
	public void Layout_round_trips()
	{
		var manager = new SettingsManager(_settings, _ibkr);
		var layout = manager.GetLayout();
		layout.LeftWidth = 333;
		layout.ChartTimeframe = ChartTimeframe.OneHour;

		manager.SaveLayout(layout);

		Assert.Equal(
			(333d, ChartTimeframe.OneHour),
			(manager.GetLayout().LeftWidth, manager.GetLayout().ChartTimeframe)
		);
	}

	[Fact]
	public void Widget_placements_round_trip_including_floating_bounds()
	{
		var manager = new SettingsManager(_settings, _ibkr);
		var layout = manager.GetLayout();
		layout.Widgets =
		[
			new WidgetPlacementDto
			{
				Tool = WorkspaceTool.Chart,
				Dock = WidgetDock.Floating,
				FloatX = 10,
				FloatY = 20,
				FloatWidth = 640,
				FloatHeight = 480,
			},
			new WidgetPlacementDto { Tool = WorkspaceTool.Watchlist, Dock = WidgetDock.Right },
		];

		manager.SaveLayout(layout);

		var saved = manager.GetLayout().Widgets;
		Assert.Equal(
			[
				(WorkspaceTool.Chart, WidgetDock.Floating),
				(WorkspaceTool.Watchlist, WidgetDock.Right),
			],
			saved.Select(x => (x.Tool, x.Dock))
		);
		Assert.Equal(
			(10d, 20d, 640d, 480d),
			(saved[0].FloatX, saved[0].FloatY, saved[0].FloatWidth, saved[0].FloatHeight)
		);
	}

	[Fact]
	public void A_bottom_dock_round_trips_with_its_heights_and_expanded_widget()
	{
		var manager = new SettingsManager(_settings, _ibkr);
		var layout = manager.GetLayout();
		layout.BottomHeight = 260;
		layout.BottomWidgetId = "Messages";
		layout.Widgets =
		[
			new WidgetPlacementDto
			{
				Id = "Messages",
				Tool = WorkspaceTool.Messages,
				Dock = WidgetDock.Bottom,
				BottomHeight = 300,
			},
		];

		manager.SaveLayout(layout);

		var saved = manager.GetLayout();
		Assert.Equal((260d, "Messages"), (saved.BottomHeight, saved.BottomWidgetId));
		var placement = Assert.Single(saved.Widgets);
		Assert.Equal((WidgetDock.Bottom, 300d), (placement.Dock, placement.BottomHeight));
	}

	[Fact]
	public void Center_views_round_trip_with_their_trees_and_the_selected_one()
	{
		var manager = new SettingsManager(_settings, _ibkr);
		var layout = manager.GetLayout();
		layout.Views =
		[
			new CenterViewDto
			{
				Id = "main",
				Name = "Main",
				Root = new CenterNodeDto
				{
					Orientation = SplitOrientation.Vertical,
					Children =
					[
						new() { WidgetId = "Chart", Size = 2 },
						new() { WidgetId = "News" },
					],
				},
			},
			new CenterViewDto { Id = "empty", Name = "Scalping" },
		];
		layout.SelectedViewId = "empty";

		manager.SaveLayout(layout);

		var saved = manager.GetLayout();
		Assert.Equal("empty", saved.SelectedViewId);
		Assert.Equal(
			[("main", "Main"), ("empty", "Scalping")],
			saved.Views.Select(x => (x.Id, x.Name))
		);
		Assert.Null(saved.Views[1].Root);
		var root = saved.Views[0].Root!;
		Assert.Equal(SplitOrientation.Vertical, root.Orientation);
		Assert.Equal(
			[("Chart", 2d), ("News", 1d)],
			root.Children.Select(x => (x.WidgetId!, x.Size))
		);
	}

	[Fact]
	public void A_layout_saved_before_views_loads_its_center_tree_and_no_views()
	{
		_settings.Settings.Layout.Center = new CenterNodeEntity { WidgetId = "Chart" };

		var layout = new SettingsManager(_settings, _ibkr).GetLayout();

		// The workspace turns the old tree into the first view (CenterViewHelpers.Normalize)
		Assert.Empty(layout.Views);
		Assert.Equal("Chart", layout.Center?.WidgetId);
	}

	[Fact]
	public void Layout_style_is_flush_by_default_and_saves_cards()
	{
		var manager = new SettingsManager(_settings, _ibkr);
		Assert.Equal(LayoutStyle.Flush, manager.GetAppSettings().LayoutStyle);
		LayoutStyle? raised = null;
		manager.AppSettingsChanged += x => raised = x.LayoutStyle;

		manager.SaveAppSettings(
			new SaveAppSettingsRequest
			{
				MarketDataType = MarketDataType.Live,
				HeartbeatSeconds = 10,
				LayoutStyle = LayoutStyle.Cards,
			}
		);

		Assert.Equal(LayoutStyle.Cards, manager.GetAppSettings().LayoutStyle);
		Assert.Equal(LayoutStyle.Cards, raised);
		// A saved Cards is not overridden by the Flush default when settings are read again
		Assert.Equal(
			LayoutStyle.Cards,
			new SettingsManager(_settings, _ibkr).GetAppSettings().LayoutStyle
		);
	}

	[Fact]
	public void Unknown_layout_style_is_rejected() =>
		Assert.Throws<LSCoreBadRequestException>(() =>
			new SettingsManager(_settings, _ibkr).SaveAppSettings(
				new SaveAppSettingsRequest
				{
					MarketDataType = MarketDataType.Live,
					HeartbeatSeconds = 10,
					LayoutStyle = (LayoutStyle)99,
				}
			)
		);
}
