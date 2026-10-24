namespace LT.Contracts.Constants;

public static class AppConstants
{
	public const string AppName = "LimitlessTrading";
	public const string SettingsFileName = "settings.json";

	/// <summary>
	/// How long closing may take to disconnect and stop the Gateway before the process exits
	/// anyway (IBC itself gives the Gateway up to 20 s to stop, then it is killed).
	/// </summary>
	public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);
	public const string BarCacheDirectoryName = "bar-cache";
}
