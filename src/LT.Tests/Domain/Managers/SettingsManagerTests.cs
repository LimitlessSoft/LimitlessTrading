using LSCore.Exceptions;
using LT.Contracts.Dtos.Settings;
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
