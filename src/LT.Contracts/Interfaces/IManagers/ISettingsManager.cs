using LT.Contracts.Dtos.Flex;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Requests.Flex;
using LT.Contracts.Requests.Settings;

namespace LT.Contracts.Interfaces.IManagers;

public interface ISettingsManager
{
	/// <summary>Raised after <see cref="SaveAppSettings"/>.</summary>
	event Action<AppSettingsDto>? AppSettingsChanged;

	/// <summary>Raised after <see cref="SaveFlexSettings"/> changed something.</summary>
	event Action<FlexSettingsDto>? FlexSettingsChanged;

	AppSettingsDto GetAppSettings();
	void SaveAppSettings(SaveAppSettingsRequest request);
	FlexSettingsDto GetFlexSettings();

	/// <summary>Validates and saves the Flex token and query ids (trimmed); all empty turns Flex off.</summary>
	void SaveFlexSettings(SaveFlexSettingsRequest request);
	LayoutDto GetLayout();
	void SaveLayout(LayoutDto layout);
	string? GetSelectedAccount();
	void SaveSelectedAccount(string? account);

	/// <summary>Privacy mode: sensitive values (accounts, balances, P&amp;L) show masked.</summary>
	bool GetHideSensitiveValues();
	void SaveHideSensitiveValues(bool hide);
}
