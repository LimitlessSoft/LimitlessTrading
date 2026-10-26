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

	/// <summary>The cache: one folder per CacheType, one JSON file per entry.</summary>
	public static string GetCacheDirectory() =>
		Path.Combine(GetAppDataDirectory(), CacheConstants.CacheDirectoryName);

	public static string GetLegacyBarCacheDirectory() =>
		Path.Combine(GetAppDataDirectory(), CacheConstants.LegacyBarCacheDirectoryName);

	/// <summary>Where "Install IBC" puts IBC: ~/.config/LimitlessTrading/ibc and the like.</summary>
	public static string GetIbcInstallDirectory() =>
		Path.Combine(GetAppDataDirectory(), GatewayConstants.IbcInstallDirectoryName);

	/// <summary>Temporary IBC config holding the password; deleted once Gateway has logged in.</summary>
	public static string GetIbcConfigFilePath() =>
		Path.Combine(GetAppDataDirectory(), GatewayConstants.IbcConfigFileName);
}
