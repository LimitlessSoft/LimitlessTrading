namespace LT.Contracts.Interfaces.IProcesses;

/// <summary>
/// File-system access for finding IB Gateway installs. Implemented in LT.Ibkr; the search logic
/// itself lives in GatewayInstallHelpers so it can be tested with a fake.
/// </summary>
public interface IIbInstallProbe
{
	bool FileExists(string path);

	/// <summary>Immediate sub-directory names of <paramref name="path"/>; empty if it doesn't exist.</summary>
	IReadOnlyList<string> GetDirectoryNames(string path);

	/// <summary>Names of the files directly in <paramref name="path"/>; empty if it doesn't exist.</summary>
	IReadOnlyList<string> GetFileNames(string path);
}
