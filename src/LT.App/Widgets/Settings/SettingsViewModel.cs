using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.App.ViewModels;
using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Settings;

namespace LT.App.Widgets.Settings;

public partial class SettingsViewModel : ViewModelBase
{
	private readonly ISettingsManager _settingsManager;

	public SettingsViewModel(ISettingsManager settingsManager)
	{
		_settingsManager = settingsManager;
		var settings = settingsManager.GetAppSettings();
		MarketDataType = settings.MarketDataType;
		AutoReconnect = settings.AutoReconnect;
		HeartbeatSeconds = settings.HeartbeatSeconds;
	}

	public IReadOnlyList<MarketDataType> MarketDataTypes { get; } =
		Enum.GetValues<MarketDataType>();
	public int HeartbeatSecondsMin => ConnectionConstants.HeartbeatSecondsMin;
	public int HeartbeatSecondsMax => ConnectionConstants.HeartbeatSecondsMax;

	[ObservableProperty]
	public partial MarketDataType MarketDataType { get; set; }

	[ObservableProperty]
	public partial bool AutoReconnect { get; set; }

	[ObservableProperty]
	public partial decimal? HeartbeatSeconds { get; set; }

	[ObservableProperty]
	public partial string? ErrorMessage { get; set; }

	/// <summary>Returns true when the settings were saved and the dialog can close.</summary>
	public bool Save()
	{
		try
		{
			_settingsManager.SaveAppSettings(
				new SaveAppSettingsRequest
				{
					MarketDataType = MarketDataType,
					AutoReconnect = AutoReconnect,
					HeartbeatSeconds = (int)(HeartbeatSeconds ?? 0),
				}
			);
			return true;
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
			return false;
		}
	}
}
