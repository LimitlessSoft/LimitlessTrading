using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Repository.Repositories;

namespace LT.Tests.Repository;

/// <summary>The real news/*.jsonl files, in a temp folder deleted after each test.</summary>
public sealed class NewsArchiveRepositoryTests : IDisposable
{
	private static readonly DateOnly Day = new(2026, 10, 29);
	private readonly string _directory = Path.Combine(
		Path.GetTempPath(),
		$"lt-news-repo-tests-{Guid.NewGuid():N}"
	);
	private readonly NewsArchiveRepository _repository;

	public NewsArchiveRepositoryTests() => _repository = new NewsArchiveRepository(_directory);

	public void Dispose()
	{
		if (Directory.Exists(_directory))
			Directory.Delete(_directory, recursive: true);
	}

	private static NewsHeadlineEntity Entity(string id) =>
		new()
		{
			SourceId = "lt",
			ProviderCode = "PRN",
			ArticleId = id,
			Time = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero),
			Headline = $"Headline {id}",
			Symbols = ["KMB"],
		};

	[Fact]
	public void An_empty_archive_has_nothing()
	{
		Assert.Empty(_repository.GetDays());
		Assert.Empty(_repository.GetHeadlines(Day));
		Assert.Equal(0, _repository.GetSizeBytes());
	}

	[Fact]
	public void Headlines_are_appended_one_line_each_per_day()
	{
		_repository.AppendHeadlines(Day, [Entity("1")]);
		_repository.AppendHeadlines(Day, [Entity("2"), Entity("3")]);
		_repository.AppendHeadlines(Day.AddDays(-1), [Entity("0")]);

		Assert.Equal([Day.AddDays(-1), Day], _repository.GetDays());
		var read = new NewsArchiveRepository(_directory).GetHeadlines(Day);
		Assert.Equal(["1", "2", "3"], read.Select(x => x.ArticleId));
		Assert.Equal(["KMB"], read[0].Symbols);
		Assert.Equal(3, File.ReadAllLines(Path.Combine(_directory, "2026-10-29.jsonl")).Length);
	}

	[Fact]
	public void A_line_cut_short_by_a_crash_is_skipped_and_the_next_lines_stay_readable()
	{
		_repository.AppendHeadlines(Day, [Entity("1")]);
		File.AppendAllText(
			Path.Combine(_directory, "2026-10-29.jsonl"),
			"{\"SourceId\":\"lt\",\"Prov"
		);

		_repository.AppendHeadlines(Day, [Entity("2")]);

		Assert.Equal(["1", "2"], _repository.GetHeadlines(Day).Select(x => x.ArticleId));
	}

	[Fact]
	public void Articles_are_kept_by_day_and_go_with_their_day()
	{
		_repository.AppendHeadlines(Day, [Entity("1")]);
		_repository.AppendArticle(
			Day,
			new NewsArticleEntity
			{
				Key = "k",
				Kind = NewsArticleKind.Text,
				Body = "text",
			}
		);

		Assert.Equal("text", Assert.Single(_repository.GetArticles(Day)).Body);
		Assert.True(_repository.GetSizeBytes() > 0);

		_repository.DeleteDay(Day);
		Assert.Empty(_repository.GetDays());
		Assert.Empty(_repository.GetArticles(Day));
	}

	[Fact]
	public void Clear_deletes_everything()
	{
		_repository.AppendHeadlines(Day, [Entity("1")]);
		_repository.AppendArticle(Day, new NewsArticleEntity { Key = "k" });

		_repository.Clear();

		Assert.Empty(_repository.GetDays());
		Assert.Equal(0, _repository.GetSizeBytes());
	}
}
