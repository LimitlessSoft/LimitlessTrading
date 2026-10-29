using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LT.Contracts.Constants;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Repository.Repositories;

/// <summary>
/// The local news archive in the app-data folder: <c>news/yyyy-MM-dd.jsonl</c> (headlines) and
/// <c>news/articles/yyyy-MM-dd.jsonl</c> (articles), one JSON object per line, only ever appended
/// to, so a crash loses at most the line being written; a line that can't be read is skipped.
/// Registered as a singleton.
/// </summary>
public class NewsArchiveRepository : INewsArchiveRepository
{
	private const string DayFormat = "yyyy-MM-dd";
	private const string Extension = ".jsonl";
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};
	private readonly Lock _lock = new();
	private readonly string _directory;
	private readonly string _articlesDirectory;

	public NewsArchiveRepository()
		: this(
			Path.Combine(AppPathHelpers.GetAppDataDirectory(), NewsConstants.ArchiveDirectoryName)
		) { }

	public NewsArchiveRepository(string directory)
	{
		_directory = directory;
		_articlesDirectory = Path.Combine(directory, NewsConstants.ArticlesDirectoryName);
	}

	public IReadOnlyList<DateOnly> GetDays()
	{
		lock (_lock)
		{
			if (!Directory.Exists(_directory))
				return [];
			return Directory
				.EnumerateFiles(_directory, "*" + Extension)
				.Select(x =>
					DateOnly.TryParseExact(
						Path.GetFileNameWithoutExtension(x),
						DayFormat,
						CultureInfo.InvariantCulture,
						DateTimeStyles.None,
						out var day
					)
						? day
						: (DateOnly?)null
				)
				.OfType<DateOnly>()
				.Order()
				.ToList();
		}
	}

	public IReadOnlyList<NewsHeadlineEntity> GetHeadlines(DateOnly day) =>
		Read<NewsHeadlineEntity>(GetPath(_directory, day));

	public void AppendHeadlines(DateOnly day, IReadOnlyList<NewsHeadlineEntity> headlines) =>
		Append(_directory, day, headlines);

	public IReadOnlyList<NewsArticleEntity> GetArticles(DateOnly day) =>
		Read<NewsArticleEntity>(GetPath(_articlesDirectory, day));

	public void AppendArticle(DateOnly day, NewsArticleEntity article) =>
		Append(_articlesDirectory, day, [article]);

	public void DeleteDay(DateOnly day)
	{
		lock (_lock)
		{
			File.Delete(GetPath(_directory, day));
			File.Delete(GetPath(_articlesDirectory, day));
		}
	}

	public long GetSizeBytes()
	{
		lock (_lock)
		{
			if (!Directory.Exists(_directory))
				return 0;
			return Directory
				.EnumerateFiles(_directory, "*", SearchOption.AllDirectories)
				.Sum(x => new FileInfo(x).Length);
		}
	}

	public void Clear()
	{
		lock (_lock)
		{
			if (Directory.Exists(_directory))
				Directory.Delete(_directory, recursive: true);
		}
	}

	private List<T> Read<T>(string path)
	{
		lock (_lock)
		{
			if (!File.Exists(path))
				return [];
			var items = new List<T>();
			foreach (var line in File.ReadLines(path))
			{
				if (string.IsNullOrWhiteSpace(line))
					continue;
				try
				{
					if (JsonSerializer.Deserialize<T>(line, JsonOptions) is { } item)
						items.Add(item);
				}
				catch (JsonException)
				{
					// A line cut short by a crash: the rest of the day is still good
				}
			}
			return items;
		}
	}

	private void Append<T>(string directory, DateOnly day, IReadOnlyList<T> items)
	{
		if (items.Count == 0)
			return;
		var text = new StringBuilder();
		foreach (var item in items)
			text.Append(JsonSerializer.Serialize(item, JsonOptions)).Append('\n');
		lock (_lock)
		{
			Directory.CreateDirectory(directory);
			using var stream = new FileStream(
				GetPath(directory, day),
				FileMode.OpenOrCreate,
				FileAccess.ReadWrite,
				FileShare.Read
			);
			// A line cut short by a crash gets its own end, so the new lines stay readable
			if (stream.Length > 0)
			{
				stream.Seek(-1, SeekOrigin.End);
				if (stream.ReadByte() != '\n')
					text.Insert(0, '\n');
			}
			stream.Seek(0, SeekOrigin.End);
			var bytes = Encoding.UTF8.GetBytes(text.ToString());
			stream.Write(bytes);
		}
	}

	private static string GetPath(string directory, DateOnly day) =>
		Path.Combine(directory, day.ToString(DayFormat, CultureInfo.InvariantCulture) + Extension);
}
