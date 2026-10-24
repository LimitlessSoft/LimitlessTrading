using LT.Contracts.Dtos.Settings;
using LT.Contracts.Requests.Settings;

namespace LT.Contracts.Interfaces.IManagers;

public interface ISettingsManager
{
	/// <summary>Raised after <see cref="SaveAppSettings"/>.</summary>
	event Action<AppSettingsDto>? AppSettingsChanged;

	AppSettingsDto GetAppSettings();
	void SaveAppSettings(SaveAppSettingsRequest request);
	LayoutDto GetLayout();
	void SaveLayout(LayoutDto layout);
	string? GetSelectedAccount();
	void SaveSelectedAccount(string? account);
}
