using LSCore.Mapper.Domain;
using LSCore.Validation.Domain;
using LT.Contracts.Dtos.Flex;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;
using LT.Contracts.Requests.Flex;
using LT.Contracts.Requests.Settings;

namespace LT.Domain.Managers;

/// <summary>Registered as a singleton so AppSettingsChanged reaches every listener.</summary>
public class SettingsManager(ISettingsRepository settingsRepository, IIbkrApiClient ibkrApiClient)
	: ISettingsManager
{
	public event Action<AppSettingsDto>? AppSettingsChanged;
	public event Action<FlexSettingsDto>? FlexSettingsChanged;

	public AppSettingsDto GetAppSettings() =>
		settingsRepository.Get().General.ToMapped<GeneralSettingsEntity, AppSettingsDto>();

	public void SaveAppSettings(SaveAppSettingsRequest request)
	{
		request.Validate();
		var settings = settingsRepository.Get();
		settings.General.MarketDataType = request.MarketDataType;
		settings.General.AutoReconnect = request.AutoReconnect;
		settings.General.HeartbeatSeconds = request.HeartbeatSeconds;
		settings.General.StopGatewayOnExit = request.StopGatewayOnExit;
		settings.General.CheckForUpdates = request.CheckForUpdates;
		settings.General.LayoutStyle = request.LayoutStyle;
		settings.General.CacheEnabled = request.CacheEnabled;
		settings.General.CacheChartBars = request.CacheChartBars;
		settings.General.CacheContracts = request.CacheContracts;
		settings.General.CacheOptionChains = request.CacheOptionChains;
		settings.General.CacheExecutions = request.CacheExecutions;
		settings.General.CacheSnapshots = request.CacheSnapshots;
		settings.General.CacheMaxSizeMb = request.CacheMaxSizeMb;
		settingsRepository.Save(settings);
		if (ibkrApiClient.IsConnected)
			ibkrApiClient.SetMarketDataType(request.MarketDataType);
		AppSettingsChanged?.Invoke(
			settings.General.ToMapped<GeneralSettingsEntity, AppSettingsDto>()
		);
	}

	public FlexSettingsDto GetFlexSettings() =>
		settingsRepository.Get().Flex.ToMapped<FlexSettingsEntity, FlexSettingsDto>();

	public void SaveFlexSettings(SaveFlexSettingsRequest request)
	{
		// Pasted values: a token with stray spaces, a query URL or "Query ID: 123456"
		request.Token = FlexHelpers.NormalizeToken(request.Token);
		request.ActivityQueryId = FlexHelpers.ExtractQueryId(request.ActivityQueryId);
		request.TradeConfirmQueryId = FlexHelpers.ExtractQueryId(request.TradeConfirmQueryId);
		request.Validate();

		var settings = settingsRepository.Get();
		var flex = settings.Flex;
		if (
			flex.Token == request.Token
			&& flex.ActivityQueryId == request.ActivityQueryId
			&& flex.TradeConfirmQueryId == request.TradeConfirmQueryId
		)
			return;
		flex.Token = request.Token;
		flex.ActivityQueryId = request.ActivityQueryId;
		flex.TradeConfirmQueryId = request.TradeConfirmQueryId;
		settingsRepository.Save(settings);
		FlexSettingsChanged?.Invoke(flex.ToMapped<FlexSettingsEntity, FlexSettingsDto>());
	}

	public LayoutDto GetLayout() =>
		settingsRepository.Get().Layout.ToMapped<LayoutEntity, LayoutDto>();

	public void SaveLayout(LayoutDto layout)
	{
		var settings = settingsRepository.Get();
		settings.Layout = layout.ToMapped<LayoutDto, LayoutEntity>();
		settingsRepository.Save(settings);
	}

	public string? GetSelectedAccount() => settingsRepository.Get().SelectedAccount;

	public void SaveSelectedAccount(string? account)
	{
		var settings = settingsRepository.Get();
		settings.SelectedAccount = account;
		settingsRepository.Save(settings);
	}
}
