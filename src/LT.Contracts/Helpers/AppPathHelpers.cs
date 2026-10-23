using LT.Contracts.Constants;

namespace LT.Contracts.Helpers;

public static class AppPathHelpers
{
	/// <summary>%APPDATA%\LimitlessTrading on Windows, ~/.config/LimitlessTrading on Linux and macOS.</summary>
	public static string GetAppDataDirectory() =>
		Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			AppConstants.AppName
		);

	public static string GetSettingsFilePath() =>
		Path.Combine(GetAppDataDirectory(), AppConstants.SettingsFileName);
}
