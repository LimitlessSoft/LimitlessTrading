using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
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
	public void Charts_follow_5_second_bars_by_default()
	{
		var settings = new SettingsManager(_settings, _ibkr).GetAppSettings();

		Assert.Equal(
			(ChartLiveUpdateMode.RealtimeBars, ChartConstants.DefaultRedrawIntervalMs),
			(settings.ChartLiveUpdateMode, settings.ChartRedrawIntervalMs)
		);
	}

	[Fact]
	public void Chart_live_update_settings_round_trip_and_are_announced()
	{
		var manager = new SettingsManager(_settings, _ibkr);
		AppSettingsDto? raised = null;
		manager.AppSettingsChanged += x => raised = x;

		manager.SaveAppSettings(
			new SaveAppSettingsRequest
			{
				MarketDataType = MarketDataType.Delayed,
				HeartbeatSeconds = 10,
				ChartLiveUpdateMode = ChartLiveUpdateMode.QuotesThrottled,
				ChartRedrawIntervalMs = 500,
			}
		);

		var settings = manager.GetAppSettings();
		Assert.Equal(
			(ChartLiveUpdateMode.QuotesThrottled, 500),
			(settings.ChartLiveUpdateMode, settings.ChartRedrawIntervalMs)
		);
		Assert.Equal(ChartLiveUpdateMode.QuotesThrottled, raised?.ChartLiveUpdateMode);
	}

	[Theory]
	[InlineData(ChartConstants.RedrawIntervalMsMin - 1)]
	[InlineData(ChartConstants.RedrawIntervalMsMax + 1)]
	public void Chart_redraw_interval_outside_the_limits_is_rejected(int intervalMs) =>
		Assert.Throws<LSCoreBadRequestException>(() =>
			new SettingsManager(_settings, _ibkr).SaveAppSettings(
				new SaveAppSettingsRequest
				{
					MarketDataType = MarketDataType.Delayed,
					HeartbeatSeconds = 10,
					ChartRedrawIntervalMs = intervalMs,
				}
			)
		);

	[Fact]
	public void Unknown_chart_live_update_mode_is_rejected() =>
		Assert.Throws<LSCoreBadRequestException>(() =>
			new SettingsManager(_settings, _ibkr).SaveAppSettings(
				new SaveAppSettingsRequest
				{
					MarketDataType = MarketDataType.Delayed,
					HeartbeatSeconds = 10,
					ChartLiveUpdateMode = (ChartLiveUpdateMode)99,
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
	public void Workspace_windows_round_trip_with_their_own_workspace_and_bounds()
	{
		var manager = new SettingsManager(_settings, _ibkr);
		var layout = manager.GetLayout();
		layout.Windows =
		[
			new WorkspaceWindowDto
			{
				Id = "w2",
				Name = "Window 2",
				X = -1200,
				Y = 80,
				Width = 1100,
				Height = 720,
				LeftWidth = 280,
				LeftWidgetId = "Positions",
				Widgets =
				[
					new WidgetPlacementDto
					{
						Id = "Positions",
						Tool = WorkspaceTool.Positions,
						Dock = WidgetDock.Left,
					},
					new WidgetPlacementDto
					{
						Id = "Chart",
						Tool = WorkspaceTool.Chart,
						Dock = WidgetDock.Center,
					},
				],
				Views =
				[
					new CenterViewDto
					{
						Id = "v",
						Name = "Main",
						Root = new CenterNodeDto { WidgetId = "Chart" },
					},
				],
				SelectedViewId = "v",
			},
		];

		manager.SaveLayout(layout);

		var saved = manager.GetLayout();
		var window = Assert.Single(saved.Windows);
		Assert.Equal(
			("w2", "Window 2", -1200d, 80d, 1100d, 720d, false),
			(
				window.Id,
				window.Name,
				window.X,
				window.Y,
				window.Width,
				window.Height,
				window.IsMaximized
			)
		);
		Assert.Equal(
			(280d, "Positions", "v"),
			(window.LeftWidth, window.LeftWidgetId, window.SelectedViewId)
		);
		Assert.Equal(
			[WorkspaceTool.Positions, WorkspaceTool.Chart],
			window.Widgets.Select(x => x.Tool)
		);
		Assert.Equal("Chart", Assert.Single(window.Views).Root?.WidgetId);
		// The main window's own workspace is untouched
		Assert.Equal(layout.Widgets.Select(x => x.Id), saved.Widgets.Select(x => x.Id));
	}

	[Fact]
	public void Window_placements_round_trip_for_the_main_workspace_and_floating_windows()
	{
		var manager = new SettingsManager(_settings, _ibkr);
		var layout = manager.GetLayout();
		var screen = new WindowScreenDto
		{
			Name = "HDMI-1",
			X = 1920,
			Width = 2560,
			Height = 1440,
		};
		WindowPlacementHelpers.SetMain(
			layout,
			new WindowPlacementDto
			{
				X = 2000,
				Y = 100,
				Width = 1400,
				Height = 900,
				State = WindowPlacementState.Maximized,
				Screen = screen,
			}
		);
		layout.Widgets =
		[
			new WidgetPlacementDto
			{
				Id = "News",
				Tool = WorkspaceTool.News,
				Dock = WidgetDock.Floating,
				FloatX = 2100,
				FloatY = 200,
				FloatWidth = 500,
				FloatHeight = 400,
				FloatState = WindowPlacementState.Maximized,
				FloatScreen = screen,
			},
		];
		var window = new WorkspaceWindowDto { Id = "w2", Name = "Window 2" };
		WindowPlacementHelpers.SetWindow(
			window,
			new WindowPlacementDto
			{
				X = 40,
				Y = 30,
				Width = 1000,
				Height = 700,
				State = WindowPlacementState.FullScreen,
				Screen = new WindowScreenDto
				{
					Name = "DP-1",
					Width = 1920,
					Height = 1080,
				},
			}
		);
		layout.Windows = [window];

		manager.SaveLayout(layout);

		var saved = manager.GetLayout();
		var main = WindowPlacementHelpers.GetMain(saved);
		Assert.Equal(
			(2000d, 100d, 1400d, 900d, WindowPlacementState.Maximized, true),
			(main.X, main.Y, main.Width, main.Height, main.State, saved.IsMaximized)
		);
		Assert.Equal(
			("HDMI-1", 1920d, 0d, 2560d, 1440d),
			(
				main.Screen?.Name,
				main.Screen?.X,
				main.Screen?.Y,
				main.Screen?.Width,
				main.Screen?.Height
			)
		);
		var floating = Assert.Single(saved.Widgets);
		Assert.Equal(
			(2100d, 200d, 500d, 400d, WindowPlacementState.Maximized, "HDMI-1"),
			(
				floating.FloatX,
				floating.FloatY,
				floating.FloatWidth,
				floating.FloatHeight,
				floating.FloatState,
				floating.FloatScreen?.Name
			)
		);
		var other = WindowPlacementHelpers.GetWindow(Assert.Single(saved.Windows));
		Assert.Equal(
			(40d, 30d, 1000d, 700d, WindowPlacementState.FullScreen, "DP-1"),
			(other.X, other.Y, other.Width, other.Height, other.State, other.Screen?.Name)
		);
	}

	[Fact]
	public void A_layout_saved_before_window_placement_loads_with_the_old_default_placement()
	{
		_settings.Settings.Layout = new LayoutEntity
		{
			WindowWidth = 1300,
			WindowHeight = 850,
			IsMaximized = true,
			Windows =
			[
				new WorkspaceWindowEntity
				{
					Id = "w2",
					Name = "Window 2",
					X = 1920,
					Y = 40,
					Width = 1200,
					Height = 760,
				},
			],
		};

		var layout = new SettingsManager(_settings, _ibkr).GetLayout();

		// Main: centered (no position) and maximized as IsMaximized said; no screen to look for
		var main = WindowPlacementHelpers.GetMain(layout);
		Assert.Equal(
			(null, null, 1300d, 850d, WindowPlacementState.Maximized, null),
			(main.X, main.Y, main.Width, main.Height, main.State, main.Screen)
		);
		var window = WindowPlacementHelpers.GetWindow(Assert.Single(layout.Windows));
		Assert.Equal(
			(1920d, 40d, 1200d, 760d, WindowPlacementState.Normal, null),
			(window.X, window.Y, window.Width, window.Height, window.State, window.Screen)
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
