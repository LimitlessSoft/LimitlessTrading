using LT.Contracts.Dtos.Gateway;

namespace LT.Contracts.Interfaces.IProcesses;

/// <summary>GitHub release lookup and zip install for IBC. Implemented in LT.Ibkr.</summary>
public interface IIbcDownloader
{
	Task<IbcReleaseDto> GetLatestReleaseAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Downloads the zip and extracts it into <paramref name="targetDirectory"/> (replacing what's
	/// there), making the shell scripts executable on Unix.
	/// </summary>
	Task InstallAsync(
		IbcReleaseAssetDto asset,
		string targetDirectory,
		CancellationToken cancellationToken = default
	);
}
