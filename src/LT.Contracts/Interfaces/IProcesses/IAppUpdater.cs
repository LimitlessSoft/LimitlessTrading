using LT.Contracts.Dtos.Updates;

namespace LT.Contracts.Interfaces.IProcesses;

/// <summary>
/// The running app's own version, its latest release on GitHub, and replacing this copy with it.
/// Implemented in LT.App.
/// </summary>
public interface IAppUpdater
{
	/// <summary>Version of the running app, e.g. "0.1.0" ("0.0.0-dev" from source).</summary>
	string CurrentVersion { get; }

	/// <summary>File name ending of this system's release archive, e.g. "-linux-x64.tar.gz".</summary>
	string AssetSuffix { get; }

	/// <summary>Why this copy can't replace itself (null: it can).</summary>
	string? CannotInstallReason { get; }

	Task<AppReleaseDto> GetLatestReleaseAsync(CancellationToken cancellationToken = default);

	/// <summary>Downloads and unpacks the release next to the app folder, ready to apply.</summary>
	Task DownloadAsync(
		AppReleaseAssetDto asset,
		IProgress<double>? progress = null,
		CancellationToken cancellationToken = default
	);

	/// <summary>
	/// Swaps the downloaded version into the app folder and starts it once this process has
	/// exited; the caller then closes the app.
	/// </summary>
	void ApplyAndRestart();

	/// <summary>Removes what an earlier update left behind (the previous version's folder).</summary>
	void CleanUp();
}
