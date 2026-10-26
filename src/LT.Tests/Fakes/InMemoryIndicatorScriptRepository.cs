using LT.Contracts.Entities;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Tests.Fakes;

/// <summary>Scripts in memory; <see cref="Write"/> / <see cref="Delete"/> raise Changed like the watcher.</summary>
public class InMemoryIndicatorScriptRepository : IIndicatorScriptRepository
{
	private readonly Dictionary<string, string> _files = [];

	public event Action<IReadOnlyCollection<string>>? Changed;

	public IReadOnlyList<IndicatorWorkspaceFileEntity>? Workspace { get; private set; }
	public bool IsWatching { get; private set; }

	public string GetDirectory() => "/indicators";

	public void EnsureWorkspace(IReadOnlyList<IndicatorWorkspaceFileEntity> files) =>
		Workspace = files;

	public IReadOnlyList<IndicatorScriptEntity> GetAll()
	{
		lock (_files)
			return [.. _files.Keys.Select(Get).Where(x => x != null).Select(x => x!)];
	}

	public IndicatorScriptEntity? Get(string fileName)
	{
		lock (_files)
			return _files.TryGetValue(fileName, out var source)
				? new IndicatorScriptEntity
				{
					FileName = fileName,
					FilePath = "/indicators/" + fileName,
					Source = source,
				}
				: null;
	}

	public void StartWatching() => IsWatching = true;

	/// <summary>Adds or changes a file without raising Changed (before the manager starts).</summary>
	public void Seed(string fileName, string source)
	{
		lock (_files)
			_files[fileName] = source;
	}

	public void Write(string fileName, string source)
	{
		Seed(fileName, source);
		Changed?.Invoke([fileName]);
	}

	public void Delete(string fileName)
	{
		lock (_files)
			_files.Remove(fileName);
		Changed?.Invoke([fileName]);
	}

	public void Dispose() { }
}
