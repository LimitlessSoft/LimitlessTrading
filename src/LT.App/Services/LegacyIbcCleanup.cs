using LT.Contracts.Helpers;

namespace LT.App.Services;

/// <summary>
/// Deletes IBC's config file that the IBC-based log-in wrote to the app-data folder: it held the
/// password while Gateway logged in, so one left behind by a crash must not stay. The IBC copy
/// "Install IBC" downloaded (app-data/ibc) is left alone — a Gateway that IBC started may still
/// run from it, and IBC re-reads it on Gateway's daily restart.
/// </summary>
public static class LegacyIbcCleanup
{
	private const string ConfigFileName = "ibc-config.ini";

	public static void Run()
	{
		try
		{
			File.Delete(Path.Combine(AppPathHelpers.GetAppDataDirectory(), ConfigFileName));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// Retried on the next start
		}
	}
}
