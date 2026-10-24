using LT.Contracts.Dtos.Gateway;
using LT.Contracts.Interfaces.IProcesses;

namespace LT.Tests.Fakes;

/// <summary>Serves <see cref="Release"/>; "installing" adds the start script to <see cref="Probe"/>.</summary>
public class FakeIbcDownloader : IIbcDownloader
{
	public FakeIbInstallProbe? Probe { get; set; }
	public string? InstalledTo { get; private set; }

	public IbcReleaseDto Release { get; set; } =
		new()
		{
			Version = "3.24.2",
			Assets =
			[
				new() { Name = "IBCLinux-3.24.2.zip", DownloadUrl = "https://x/l.zip" },
				new() { Name = "IBCMacos-3.24.2.zip", DownloadUrl = "https://x/m.zip" },
				new() { Name = "IBCWin-3.24.2.zip", DownloadUrl = "https://x/w.zip" },
			],
		};

	public Task<IbcReleaseDto> GetLatestReleaseAsync(
		CancellationToken cancellationToken = default
	) => Task.FromResult(Release);

	public Task InstallAsync(
		IbcReleaseAssetDto asset,
		string targetDirectory,
		CancellationToken cancellationToken = default
	)
	{
		InstalledTo = targetDirectory;
		var script = OperatingSystem.IsWindows() ? "StartIBC.bat" : "ibcstart.sh";
		Probe?.AddFile(Path.Combine(targetDirectory, "scripts", script));
		return Task.CompletedTask;
	}
}
