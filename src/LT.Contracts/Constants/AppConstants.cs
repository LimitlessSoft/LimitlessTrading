namespace LT.Contracts.Constants;

public static class AppConstants
{
	public const string AppName = "LimitlessTrading";
	public const string SettingsFileName = "settings.json";

	/// <summary>
	/// How long closing may take to disconnect and stop the Gateway before the process exits
	/// anyway.
	/// </summary>
	public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

	/// <summary>GitHub's latest release of the app (the updater compares its tag).</summary>
	public const string LatestReleaseApiUrl =
		"https://api.github.com/repos/LimitlessSoft/LimitlessTrading/releases/latest";

	/// <summary>Version suffix of builds from source: they never update themselves.</summary>
	public const string DevVersionSuffix = "-dev";

	/// <summary>First automatic update check after start, then every <see cref="UpdateCheckInterval"/>.</summary>
	public static readonly TimeSpan UpdateStartupDelay = TimeSpan.FromSeconds(10);
	public static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(6);

	/// <summary>Release archives are ~12 MB; anything far larger is a broken or wrong download.</summary>
	public const long UpdateMaxDownloadBytes = 300L * 1024 * 1024;
	public static readonly TimeSpan UpdateDownloadTimeout = TimeSpan.FromMinutes(10);
}
