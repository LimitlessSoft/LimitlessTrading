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

	/// <summary>
	/// The saved layout as a layout file (JSON): every window with its bounds, state and screen, its
	/// views, docked and floating widgets and their per-instance settings. Only the layout: no
	/// connection, Flex or account settings. Save the live layout first to export what is shown.
	/// </summary>
	string ExportLayout();

	/// <summary>
	/// Replaces the saved layout with the one in an exported file and returns it, to be applied.
	/// The file is checked first (ours, not too big, not damaged, not from a newer version); when
	/// it is refused nothing changes.
	/// </summary>
	LayoutDto ImportLayout(ImportLayoutRequest request);
	string? GetSelectedAccount();
	void SaveSelectedAccount(string? account);

	/// <summary>Privacy mode: sensitive values (accounts, balances, P&amp;L) show masked.</summary>
	bool GetHideSensitiveValues();
	void SaveHideSensitiveValues(bool hide);
}
