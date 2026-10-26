using LSCore.Mapper.Domain;
using LSCore.Validation.Domain;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;
using LT.Contracts.Requests.Settings;

namespace LT.Domain.Managers;

/// <summary>Registered as a singleton so AppSettingsChanged reaches every listener.</summary>
public class SettingsManager(ISettingsRepository settingsRepository, IIbkrApiClient ibkrApiClient)
	: ISettingsManager
{
	public event Action<AppSettingsDto>? AppSettingsChanged;

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
		settingsRepository.Save(settings);
		if (ibkrApiClient.IsConnected)
			ibkrApiClient.SetMarketDataType(request.MarketDataType);
		AppSettingsChanged?.Invoke(
			settings.General.ToMapped<GeneralSettingsEntity, AppSettingsDto>()
		);
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
