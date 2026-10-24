using LT.Contracts.Interfaces.IProcesses;

namespace LT.Ibkr.Ibc;

public class IbInstallProbe : IIbInstallProbe
{
	// Big trees that never hold an IBC install; keeps the home search fast
	private static readonly HashSet<string> SkippedDirectories = new(
		StringComparer.OrdinalIgnoreCase
	)
	{
		"node_modules",
		"bin",
		"obj",
		"snap",
		"Library",
		"AppData",
	};

	public bool FileExists(string path) => File.Exists(path);

	public IReadOnlyList<string> GetDirectoryNames(string path)
	{
		try
		{
			return Directory.Exists(path)
				? Directory
					.EnumerateDirectories(path)
					.Select(Path.GetFileName)
					.OfType<string>()
					.ToList()
				: [];
		}
		catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
		{
			return [];
		}
	}

	public IReadOnlyList<string> FindFiles(string root, string fileName, int maxDepth)
	{
		var found = new List<string>();
		if (!Directory.Exists(root))
			return found;

		var options = new EnumerationOptions
		{
			IgnoreInaccessible = true,
			AttributesToSkip =
				FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
		};
		var pending = new Queue<(string Path, int Depth)>([(root, 0)]);
		while (pending.TryDequeue(out var current))
		{
			try
			{
				var file = Path.Combine(current.Path, fileName);
				if (File.Exists(file))
					found.Add(file);
				if (current.Depth >= maxDepth)
					continue;
				foreach (
					var directory in Directory.EnumerateDirectories(current.Path, "*", options)
				)
				{
					var name = Path.GetFileName(directory);
					if (!name.StartsWith('.') && !SkippedDirectories.Contains(name))
						pending.Enqueue((directory, current.Depth + 1));
				}
			}
			catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
			{
				// Unreadable folder: skip it
			}
		}
		return found;
	}
}
