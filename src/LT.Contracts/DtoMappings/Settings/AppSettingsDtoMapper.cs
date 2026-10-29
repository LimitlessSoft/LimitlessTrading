using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class AppSettingsDtoMapper : ILSCoreMapper<GeneralSettingsEntity, AppSettingsDto>
{
	public AppSettingsDto ToMapped(GeneralSettingsEntity source) =>
		new()
		{
			MarketDataType = source.MarketDataType,
			AutoReconnect = source.AutoReconnect,
			HeartbeatSeconds = source.HeartbeatSeconds,
			StopGatewayOnExit = source.StopGatewayOnExit,
			CheckForUpdates = source.CheckForUpdates,
			LayoutStyle = source.LayoutStyle,
			CacheEnabled = source.CacheEnabled,
			CacheChartBars = source.CacheChartBars,
			CacheContracts = source.CacheContracts,
			CacheOptionChains = source.CacheOptionChains,
			CacheExecutions = source.CacheExecutions,
			CacheSnapshots = source.CacheSnapshots,
			CacheMaxSizeMb = source.CacheMaxSizeMb,
			ChartLiveUpdateMode = source.ChartLiveUpdateMode,
			ChartRedrawIntervalMs = source.ChartRedrawIntervalMs,
			ChartFootprintImbalancePercent = source.ChartFootprintImbalancePercent,
			DisplayTimeZoneId = source.DisplayTimeZoneId,
			PlayFillSound = source.PlayFillSound,
			CalendarNasdaq = source.CalendarNasdaq,
			CalendarWallStreetHorizon = source.CalendarWallStreetHorizon,
			NewsFeeds = source.NewsFeeds,
			NewsRetentionDays = source.NewsRetentionDays,
			NewsAlertKeywords = source.NewsAlertKeywords,
			NewsAlertWatchlist = source.NewsAlertWatchlist,
			NewsAlertSound = source.NewsAlertSound,
		};
}
