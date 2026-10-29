using System.Globalization;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Repository.Repositories;

/// <summary>
/// The footprint trade cache on disk: &lt;app-data&gt;/footprint-trades/&lt;conId&gt;/&lt;yyyy-MM-dd&gt;.lttape,
/// one file per UTC day, frames appended (<see cref="FootprintTapeHelpers"/>). What each file
/// covers is read once per run (its frames checked, a torn tail cut off so appends follow the
/// last whole frame) and kept in memory to skip what is already recorded. One lock for all of it.
/// Registered as a singleton.
/// </summary>
public class FootprintTapeRepository(string directory) : IFootprintTapeRepository
{
	private readonly Lock _lock = new();

	// What each file read or written this run covers, and where its last whole frame ends
	private readonly Dictionary<string, TapeFile> _files = [];

	public FootprintTapeRepository()
		: this(AppPathHelpers.GetFootprintTapeDirectory()) { }

	public void Append(
		int conId,
		DateTimeOffset from,
		DateTimeOffset to,
		IReadOnlyList<FootprintTapeTradeDto> trades
	)
	{
		if (from >= to)
			return;
		if (!IsSorted(trades))
			trades = trades.OrderBy(x => x.UnixSeconds).ToList();
		lock (_lock)
		{
			// One file per UTC day: the stretch cut at midnight
			for (var dayFrom = from; dayFrom < to; )
			{
				var day = GetDay(dayFrom);
				var nextDay = new DateTimeOffset(day.AddDays(1), TimeSpan.Zero);
				var dayTo = to < nextDay ? to : nextDay;
				AppendToFile(GetPath(conId, day), dayFrom, dayTo, trades);
				dayFrom = dayTo;
			}
		}
	}

	public IReadOnlyList<FootprintTapeRangeDto> Get(
		int conId,
		DateTimeOffset from,
		DateTimeOffset to
	)
	{
		var frames = new List<(byte[] File, FootprintTapeFrameDto Frame)>();
		lock (_lock)
		{
			for (var day = GetDay(from); day < to.UtcDateTime; day = day.AddDays(1))
			{
				var path = GetPath(conId, day);
				try
				{
					if (!File.Exists(path))
						continue;
					var data = File.ReadAllBytes(path);
					var file = Scan(path, data, out var dayFrames);
					foreach (var frame in dayFrames)
						if (frame.End <= file.Length)
							frames.Add((data, frame));
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
			}
		}
		return FootprintTapeHelpers.ToRanges(frames);
	}

	public void Prune(DateOnly keepFrom, long maxBytes)
	{
		lock (_lock)
		{
			var files = ListFiles();
			var kept = new List<(string Path, DateOnly Day, long Bytes)>();
			foreach (var file in files)
				if (file.Day < keepFrom)
					Delete(file.Path);
				else
					kept.Add(file);
			var total = kept.Sum(x => x.Bytes);
			foreach (var file in kept.OrderBy(x => x.Day).ThenBy(x => x.Path))
			{
				if (total <= maxBytes)
					break;
				Delete(file.Path);
				total -= file.Bytes;
			}
			foreach (var folder in SafeEnumerate(() => Directory.EnumerateDirectories(directory)))
				try
				{
					if (!Directory.EnumerateFileSystemEntries(folder).Any())
						Directory.Delete(folder);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
		}
	}

	public long GetSizeBytes()
	{
		lock (_lock)
			return ListFiles().Sum(x => x.Bytes);
	}

	// Under the lock: the stretch's parts not yet in the file, each as a frame with its trades,
	// appended after the file's last whole frame; the coverage grows only once they are written
	private void AppendToFile(
		string path,
		DateTimeOffset from,
		DateTimeOffset to,
		IReadOnlyList<FootprintTapeTradeDto> trades
	)
	{
		try
		{
			var file = Open(path);
			var gaps = FootprintTapeHelpers.GetGaps(file.Covered, from, to);
			if (gaps.Count == 0)
				return;
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			using var stream = new FileStream(
				path,
				FileMode.OpenOrCreate,
				FileAccess.Write,
				FileShare.Read
			);
			if (file.Length < FootprintTapeHelpers.FileHeaderLength)
			{
				stream.SetLength(0);
				stream.Write(FootprintTapeHelpers.CreateFileHeader());
				file.Length = FootprintTapeHelpers.FileHeaderLength;
			}
			// Whatever follows the last whole frame (a torn write) is cut off
			stream.SetLength(file.Length);
			stream.Position = file.Length;
			var length = file.Length;
			foreach (var (gapFrom, gapTo) in gaps)
			{
				var start = FootprintTapeHelpers.LowerBound(trades, gapFrom.ToUnixTimeSeconds());
				var end = FootprintTapeHelpers.LowerBound(trades, gapTo.ToUnixTimeSeconds());
				var frame = FootprintTapeHelpers.EncodeFrame(gapFrom, gapTo, trades, start, end);
				stream.Write(frame);
				length += frame.Length;
			}
			stream.Flush();
			file.Length = length;
			foreach (var (gapFrom, gapTo) in gaps)
				FootprintTapeHelpers.AddCovered(file.Covered, gapFrom, gapTo);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// Read again next time: the frames that made it in are found by their checksums
			_files.Remove(path);
		}
	}

	// Under the lock: what the file covers, read once per run
	private TapeFile Open(string path)
	{
		if (_files.TryGetValue(path, out var file))
			return file;
		if (!File.Exists(path))
			return _files[path] = new TapeFile();
		return Scan(path, File.ReadAllBytes(path), out _);
	}

	// Under the lock: the file's whole frames; a torn or corrupt tail is cut off
	private TapeFile Scan(string path, byte[] data, out List<FootprintTapeFrameDto> frames)
	{
		frames = FootprintTapeHelpers.ReadFrames(data, out var validLength);
		if (validLength < data.Length)
			try
			{
				using var stream = new FileStream(path, FileMode.Open, FileAccess.Write);
				stream.SetLength(validLength);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
		var file = new TapeFile { Length = validLength };
		foreach (var frame in frames)
			FootprintTapeHelpers.AddCovered(file.Covered, frame.From, frame.To);
		_files[path] = file;
		return file;
	}

	private List<(string Path, DateOnly Day, long Bytes)> ListFiles()
	{
		var files = new List<(string Path, DateOnly Day, long Bytes)>();
		foreach (
			var path in SafeEnumerate(() =>
				Directory.EnumerateFiles(
					directory,
					"*" + FootprintConstants.TapeFileExtension,
					SearchOption.AllDirectories
				)
			)
		)
		{
			if (
				!DateOnly.TryParseExact(
					Path.GetFileNameWithoutExtension(path),
					"yyyy-MM-dd",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None,
					out var day
				)
			)
				continue;
			try
			{
				files.Add((path, day, new FileInfo(path).Length));
			}
			catch (IOException) { }
		}
		return files;
	}

	private void Delete(string path)
	{
		try
		{
			File.Delete(path);
			_files.Remove(path);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
	}

	private IEnumerable<string> SafeEnumerate(Func<IEnumerable<string>> list)
	{
		if (!Directory.Exists(directory))
			return [];
		try
		{
			return list().ToList();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return [];
		}
	}

	private string GetPath(int conId, DateTime day) =>
		Path.Combine(
			directory,
			conId.ToString(CultureInfo.InvariantCulture),
			day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
				+ FootprintConstants.TapeFileExtension
		);

	private static DateTime GetDay(DateTimeOffset time) => time.UtcDateTime.Date;

	private static bool IsSorted(IReadOnlyList<FootprintTapeTradeDto> trades)
	{
		for (var i = 1; i < trades.Count; i++)
			if (trades[i].UnixSeconds < trades[i - 1].UnixSeconds)
				return false;
		return true;
	}

	/// <summary>What one file covers and where its last whole frame ends.</summary>
	private sealed class TapeFile
	{
		public int Length { get; set; }
		public List<(DateTimeOffset From, DateTimeOffset To)> Covered { get; } = [];
	}
}
