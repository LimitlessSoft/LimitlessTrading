namespace LT.Contracts.Interfaces.IProcesses;

/// <summary>
/// File-system access for finding IBC / IB Gateway installs. Implemented in LT.Ibkr; the search
/// logic itself lives in IbcInstallHelpers so it can be tested with a fake.
/// </summary>
public interface IIbInstallProbe
{
	bool FileExists(string path);

	/// <summary>Immediate sub-directory names of <paramref name="path"/>; empty if it doesn't exist.</summary>
	IReadOnlyList<string> GetDirectoryNames(string path);

	/// <summary>
	/// Bounded search for <paramref name="fileName"/> under <paramref name="root"/>: at most
	/// <paramref name="maxDepth"/> levels, skipping hidden and inaccessible folders.
	/// </summary>
	IReadOnlyList<string> FindFiles(string root, string fileName, int maxDepth);
}
