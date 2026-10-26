using LT.Contracts.Interfaces.IProcesses;

namespace LT.Tests.Fakes;

/// <summary>In-memory file tree: add files with <see cref="AddFile"/>; directories are implied.</summary>
public class FakeIbInstallProbe : IIbInstallProbe
{
	private readonly HashSet<string> _files = [];

	public FakeIbInstallProbe AddFile(string path)
	{
		_files.Add(Normalize(path));
		return this;
	}

	public bool FileExists(string path) => _files.Contains(Normalize(path));

	public IReadOnlyList<string> GetDirectoryNames(string path)
	{
		var prefix = Normalize(path) + "/";
		return _files
			.Where(x => x.StartsWith(prefix))
			.Select(x => x[prefix.Length..].Split('/'))
			.Where(x => x.Length > 1)
			.Select(x => x[0])
			.Distinct()
			.ToList();
	}

	public IReadOnlyList<string> GetFileNames(string path)
	{
		var prefix = Normalize(path) + "/";
		return _files
			.Where(x => x.StartsWith(prefix) && !x[prefix.Length..].Contains('/'))
			.Select(x => x[prefix.Length..])
			.ToList();
	}

	private static string Normalize(string path) =>
		Path.TrimEndingDirectorySeparator(path.Replace('\\', '/'));
}
