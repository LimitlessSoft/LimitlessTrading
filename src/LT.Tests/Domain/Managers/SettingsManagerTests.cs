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
			}
		);

		Assert.True(raised);
		Assert.Equal(MarketDataType.Live, _ibkr.MarketDataType);
		var saved = manager.GetAppSettings();
		Assert.Equal(
			(MarketDataType.Live, false, 10),
			(saved.MarketDataType, saved.AutoReconnect, saved.HeartbeatSeconds)
		);
	}

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
}
