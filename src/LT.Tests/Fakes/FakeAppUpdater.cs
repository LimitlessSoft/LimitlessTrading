using LT.Contracts.Dtos.Updates;
using LT.Contracts.Interfaces.IProcesses;

namespace LT.Tests.Fakes;

/// <summary>Serves <see cref="Release"/>; records the download and the restart instead of doing them.</summary>
public class FakeAppUpdater : IAppUpdater
{
	public string CurrentVersion { get; set; } = "0.1.0";
	public string AssetSuffix { get; set; } = "-linux-x64.tar.gz";
	public string? CannotInstallReason { get; set; }
	public Exception? CheckError { get; set; }
	public Exception? DownloadError { get; set; }
	public AppReleaseAssetDto? Downloaded { get; private set; }
	public bool Restarted { get; private set; }

	public AppReleaseDto Release { get; set; } =
		new()
		{
			Tag = "v0.2.0",
			PageUrl = "https://github.com/LimitlessSoft/LimitlessTrading/releases/tag/v0.2.0",
			Assets =
			[
				new()
				{
					Name = "LimitlessTrading-0.2.0-linux-x64.tar.gz",
					DownloadUrl = "https://x/l.tar.gz",
				},
			],
		};

	public Task<AppReleaseDto> GetLatestReleaseAsync(
		CancellationToken cancellationToken = default
	) =>
		CheckError is null
			? Task.FromResult(Release)
			: Task.FromException<AppReleaseDto>(CheckError);

	public Task DownloadAsync(
		AppReleaseAssetDto asset,
		IProgress<double>? progress = null,
		CancellationToken cancellationToken = default
	)
	{
		if (DownloadError != null)
			return Task.FromException(DownloadError);
		Downloaded = asset;
		return Task.CompletedTask;
	}

	public void ApplyAndRestart() => Restarted = true;

	public void CleanUp() { }
}
