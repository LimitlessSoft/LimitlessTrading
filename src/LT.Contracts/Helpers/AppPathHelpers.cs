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

	/// <summary>
	/// The footprint trade cache: one folder per contract id, one file per UTC day
	/// (<c>FootprintTapeHelpers</c>).
	/// </summary>
	public static string GetFootprintTapeDirectory() =>
		Path.Combine(GetAppDataDirectory(), FootprintConstants.TapeDirectoryName);

	/// <summary>Recorded account performance: one JSON file per account.</summary>
	public static string GetAccountHistoryDirectory() =>
		Path.Combine(GetAppDataDirectory(), AccountPerformanceConstants.HistoryDirectoryName);

	/// <summary>Indicator scripts: one <c>*.cs</c> file per indicator, plus the editor project.</summary>
	public static string GetIndicatorsDirectory() =>
		Path.Combine(GetAppDataDirectory(), IndicatorConstants.DirectoryName);

	/// <summary>Sounds the app generated for the system player (<c>SoundHelpers</c>).</summary>
	public static string GetSoundsDirectory() =>
		Path.Combine(GetAppDataDirectory(), SoundConstants.DirectoryName);
}
