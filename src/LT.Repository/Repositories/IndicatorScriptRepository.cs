using LT.Contracts.Constants;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Repository.Repositories;

/// <summary>
/// Indicator scripts as <c>*.cs</c> files in the indicators folder (app-data folder, see
/// <see cref="AppPathHelpers.GetIndicatorsDirectory"/>). A <see cref="FileSystemWatcher"/> on the
/// folder collects changed file names and raises <see cref="Changed"/> once they have been
/// quiet for <see cref="IndicatorConstants.ReloadDebounce"/> (editors save in several steps:
/// temp file, rename, touch). When the watcher can't run or reports an error (the inotify
/// limit on Linux, a network drive, an overflowing event buffer) the folder is scanned every
/// <see cref="IndicatorConstants.PollInterval"/> instead and differences are reported the same
/// way. Registered as a singleton.
/// </summary>
public class IndicatorScriptRepository : IIndicatorScriptRepository
{
	private readonly Lock _lock = new();
	private readonly string _directory;
	private readonly TimeProvider _timeProvider;
	private readonly HashSet<string> _pending = new(StringComparer.Ordinal);
	private FileSystemWatcher? _watcher;
	private ITimer? _timer;
	private ITimer? _poll;
	private Dictionary<string, (long Length, DateTime Modified)> _snapshot = [];
	private bool _disposed;

	public IndicatorScriptRepository(TimeProvider timeProvider)
		: this(timeProvider, AppPathHelpers.GetIndicatorsDirectory()) { }

	public IndicatorScriptRepository(TimeProvider timeProvider, string directory)
	{
		_timeProvider = timeProvider;
		_directory = directory;
	}

	public event Action<IReadOnlyCollection<string>>? Changed;

	public string GetDirectory() => _directory;

	public void EnsureWorkspace(IReadOnlyList<IndicatorWorkspaceFileEntity> files)
	{
		var firstRun = !Directory.Exists(_directory);
		Directory.CreateDirectory(_directory);
		foreach (var file in files)
		{
			var path = Path.Combine(_directory, file.RelativePath);
			try
			{
				if (file.FirstRunOnly && !firstRun || file.KeepExisting && File.Exists(path))
					continue;
				Directory.CreateDirectory(Path.GetDirectoryName(path)!);
				if (file.CopyFrom is { } source)
				{
					if (File.Exists(source) && !IsSameFile(source, path))
						File.Copy(source, path, overwrite: true);
				}
				else if (file.Content != null)
					File.WriteAllText(path, file.Content);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// A locked SDK copy (an editor holds it) is refreshed on the next start
			}
		}
	}

	public IReadOnlyList<IndicatorScriptEntity> GetAll()
	{
		if (!Directory.Exists(_directory))
			return [];
		return
		[
			.. Directory
				.EnumerateFiles(_directory, "*" + IndicatorConstants.ScriptExtension)
				.Select(Path.GetFileName)
				.Where(x => IsScriptFile(x!))
				.Select(x => Get(x!))
				.Where(x => x != null)
				.Select(x => x!)
				.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase),
		];
	}

	public IndicatorScriptEntity? Get(string fileName)
	{
		if (!IsScriptFile(fileName))
			return null;
		var path = Path.Combine(_directory, fileName);
		try
		{
			if (!File.Exists(path))
				return null;
			return new IndicatorScriptEntity
			{
				FileName = fileName,
				FilePath = path,
				Source = File.ReadAllText(path),
				ModifiedAt = File.GetLastWriteTimeUtc(path),
			};
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// Still being written: the watcher reports it again once the editor is done
			return null;
		}
	}

	public void StartWatching()
	{
		lock (_lock)
		{
			if (_watcher != null || _poll != null || _disposed)
				return;
			Directory.CreateDirectory(_directory);
			_snapshot = TakeSnapshot();
			try
			{
				_watcher = new FileSystemWatcher(
					_directory,
					"*" + IndicatorConstants.ScriptExtension
				)
				{
					NotifyFilter =
						NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
					IncludeSubdirectories = false,
				};
				_watcher.Created += OnFileEvent;
				_watcher.Changed += OnFileEvent;
				_watcher.Deleted += OnFileEvent;
				_watcher.Renamed += OnRenamed;
				_watcher.Error += OnWatcherError;
				_watcher.EnableRaisingEvents = true;
			}
			catch (Exception ex) when (ex is IOException or PlatformNotSupportedException)
			{
				StartPolling();
			}
		}
	}

	public void Dispose()
	{
		lock (_lock)
		{
			_disposed = true;
			_watcher?.Dispose();
			_watcher = null;
			_timer?.Dispose();
			_timer = null;
			_poll?.Dispose();
			_poll = null;
		}
		GC.SuppressFinalize(this);
	}

	private void OnFileEvent(object sender, FileSystemEventArgs e) => Queue(e.Name);

	private void OnRenamed(object sender, RenamedEventArgs e)
	{
		Queue(e.OldName);
		Queue(e.Name);
	}

	/// <summary>Collects a changed file and (re)starts the quiet-period timer.</summary>
	public void Queue(string? fileName)
	{
		if (fileName == null || !IsScriptFile(Path.GetFileName(fileName)))
			return;
		lock (_lock)
		{
			if (_disposed)
				return;
			_pending.Add(Path.GetFileName(fileName));
			_timer ??= _timeProvider.CreateTimer(
				_ => Flush(),
				null,
				Timeout.InfiniteTimeSpan,
				Timeout.InfiniteTimeSpan
			);
			_timer.Change(IndicatorConstants.ReloadDebounce, Timeout.InfiniteTimeSpan);
		}
	}

	private void Flush()
	{
		string[] changed;
		lock (_lock)
		{
			if (_pending.Count == 0)
				return;
			changed = [.. _pending];
			_pending.Clear();
			// What is reported now is what a later scan compares against
			var current = TakeSnapshot();
			foreach (var name in changed)
				if (current.TryGetValue(name, out var entry))
					_snapshot[name] = entry;
				else
					_snapshot.Remove(name);
		}
		Changed?.Invoke(changed);
	}

	// The watcher is gone for good (or lost events): scan instead, starting with a scan now
	private void OnWatcherError(object sender, ErrorEventArgs e)
	{
		lock (_lock)
		{
			if (_disposed)
				return;
			_watcher?.Dispose();
			_watcher = null;
			StartPolling();
		}
		Scan();
	}

	private void StartPolling() =>
		_poll ??= _timeProvider.CreateTimer(
			_ => Scan(),
			null,
			IndicatorConstants.PollInterval,
			IndicatorConstants.PollInterval
		);

	/// <summary>Queues every script added, changed or deleted since the last scan.</summary>
	public void Scan()
	{
		Dictionary<string, (long, DateTime)> current;
		List<string> changed;
		lock (_lock)
		{
			if (_disposed)
				return;
			current = TakeSnapshot();
			changed =
			[
				.. current
					.Where(x => !_snapshot.TryGetValue(x.Key, out var old) || old != x.Value)
					.Select(x => x.Key),
				.. _snapshot.Keys.Where(x => !current.ContainsKey(x)),
			];
			_snapshot = current;
		}
		foreach (var name in changed)
			Queue(name);
	}

	private Dictionary<string, (long Length, DateTime Modified)> TakeSnapshot()
	{
		var snapshot = new Dictionary<string, (long, DateTime)>(StringComparer.Ordinal);
		try
		{
			if (!Directory.Exists(_directory))
				return snapshot;
			foreach (
				var file in new DirectoryInfo(_directory).EnumerateFiles(
					"*" + IndicatorConstants.ScriptExtension
				)
			)
				if (IsScriptFile(file.Name))
					snapshot[file.Name] = (file.Length, file.LastWriteTimeUtc);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// Unreadable for now: compared again on the next scan
		}
		return snapshot;
	}

	// Hidden files and editor temp copies ("~x.cs", ".x.cs") are not scripts
	private static bool IsScriptFile(string fileName) =>
		fileName.EndsWith(IndicatorConstants.ScriptExtension, StringComparison.OrdinalIgnoreCase)
		&& !fileName.StartsWith('.')
		&& !fileName.StartsWith('~')
		&& fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
		&& Path.GetFileName(fileName) == fileName;

	private static bool IsSameFile(string source, string target)
	{
		if (!File.Exists(target))
			return false;
		var a = new FileInfo(source);
		var b = new FileInfo(target);
		return a.Length == b.Length && a.LastWriteTimeUtc <= b.LastWriteTimeUtc;
	}
}
