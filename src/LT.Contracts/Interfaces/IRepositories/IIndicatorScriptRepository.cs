using LT.Contracts.Entities;

namespace LT.Contracts.Interfaces.IRepositories;

/// <summary>
/// Indicator scripts: the <c>*.cs</c> files in the indicators folder of the app-data folder,
/// edited in any editor. Watches the folder once <see cref="StartWatching"/> is called.
/// </summary>
public interface IIndicatorScriptRepository : IDisposable
{
	/// <summary>
	/// File names (with extension) added, changed, renamed or deleted, debounced so an editor's
	/// save raises it once. Raised on a timer thread.
	/// </summary>
	event Action<IReadOnlyCollection<string>>? Changed;

	string GetDirectory();

	/// <summary>Creates the folder and writes / refreshes <paramref name="files"/> in it.</summary>
	void EnsureWorkspace(IReadOnlyList<IndicatorWorkspaceFileEntity> files);

	IReadOnlyList<IndicatorScriptEntity> GetAll();

	/// <summary>The script <paramref name="fileName"/>; null when the file is gone or unreadable.</summary>
	IndicatorScriptEntity? Get(string fileName);

	void StartWatching();
}
