using LT.Contracts.Interfaces.IProcesses;

namespace LT.Ibkr.Gateway;

public class IbInstallProbe : IIbInstallProbe
{
	public bool FileExists(string path) => File.Exists(path);

	public IReadOnlyList<string> GetDirectoryNames(string path) =>
		List(path, Directory.EnumerateDirectories);

	public IReadOnlyList<string> GetFileNames(string path) => List(path, Directory.EnumerateFiles);

	private static IReadOnlyList<string> List(
		string path,
		Func<string, IEnumerable<string>> enumerate
	)
	{
		try
		{
			return Directory.Exists(path)
				? enumerate(path).Select(Path.GetFileName).OfType<string>().ToList()
				: [];
		}
		catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
		{
			return [];
		}
	}
}
