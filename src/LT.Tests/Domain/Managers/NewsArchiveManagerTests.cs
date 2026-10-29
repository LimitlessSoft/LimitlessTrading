using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Settings;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public sealed class NewsArchiveManagerTests : IDisposable
{
	private readonly TestNewsArchive _archive = new();

	public void Dispose() => _archive.Dispose();

	private static NewsHeadlineDto Headline(
		string provider,
		string id,
		DateTimeOffset time,
		string text = "Headline",
		string source = "lt",
		params string[] symbols
	) =>
		new()
		{
			SourceId = source,
			ProviderCode = provider,
			ArticleId = id,
			Time = time,
			Headline = text,
			Symbol = symbols.FirstOrDefault() ?? string.Empty,
			Symbols = symbols,
		};

	private static DateTimeOffset At(int day, int hour = 12) =>
		new(2026, 10, day, hour, 0, 0, TimeSpan.Zero);

	private static IReadOnlySet<string> Keys(params string[] keys) => keys.ToHashSet();

	[Fact]
	public void Each_headline_is_archived_once_and_only_new_ones_are_announced()
	{
		List<IReadOnlyList<NewsHeadlineDto>> announced = [];
		_archive.Manager.HeadlinesAdded += announced.Add;

		var first = _archive.Manager.Add([
			Headline("PRN", "1", At(29)),
			Headline("PRN", "2", At(29)),
		]);
		var again = _archive.Manager.Add([
			Headline("PRN", "2", At(29)),
			Headline("PRN", "3", At(29)),
		]);

		Assert.Equal(["1", "2"], first.Select(x => x.ArticleId));
		Assert.Equal(["3"], again.Select(x => x.ArticleId));
		Assert.Equal(2, announced.Count);
		Assert.Equal(3, _archive.Repository.GetHeadlines(new DateOnly(2026, 10, 29)).Count);
	}

	[Fact]
	public void A_headline_archived_in_an_earlier_run_is_not_archived_again()
	{
		_archive.Manager.Add([Headline("PRN", "1", At(20))]);

		var restarted = new NewsArchiveManager(
			_archive.Repository,
			_archive.SettingsManager,
			_archive.Time
		);

		Assert.Empty(restarted.Add([Headline("PRN", "1", At(20))]));
	}

	[Fact]
	public void History_is_newest_first_across_days_filtered_by_provider_and_symbol()
	{
		_archive.Manager.Add([
			Headline("PRN", "a", At(27), symbols: "KMB"),
			Headline("PRN", "b", At(28), symbols: "MRP"),
			Headline("SEC", "c", At(29, 10), symbols: "KMB"),
			Headline("PRN", "d", At(29, 11), symbols: "KMB"),
			Headline("DJ-N", "e", At(29, 12), source: "ibkr", symbols: "KMB"),
		]);

		var page = _archive.Manager.GetHeadlines(
			new NewsArchiveQueryDto
			{
				ProviderKeys = Keys("lt:PRN", "lt:SEC", "ibkr:DJ-N"),
				Symbol = "kmb",
				Limit = 10,
			}
		);
		var prnOnly = _archive.Manager.GetHeadlines(
			new NewsArchiveQueryDto { ProviderKeys = Keys("lt:PRN"), Limit = 10 }
		);

		Assert.Equal(["e", "d", "c", "a"], page.Headlines.Select(x => x.ArticleId));
		Assert.False(page.HasMore);
		Assert.Equal(["d", "b", "a"], prnOnly.Headlines.Select(x => x.ArticleId));
	}

	[Fact]
	public void Paging_back_goes_before_the_oldest_shown()
	{
		for (var day = 20; day <= 29; day++)
			_archive.Manager.Add([Headline("PRN", $"{day}", At(day))]);
		var query = new NewsArchiveQueryDto { ProviderKeys = Keys("lt:PRN"), Limit = 3 };

		var first = _archive.Manager.GetHeadlines(query);
		var older = _archive.Manager.GetHeadlines(
			new NewsArchiveQueryDto
			{
				ProviderKeys = query.ProviderKeys,
				Limit = 3,
				Before = first.Headlines[^1].Time,
			}
		);

		Assert.Equal(["29", "28", "27"], first.Headlines.Select(x => x.ArticleId));
		Assert.True(first.HasMore);
		Assert.Equal(["26", "25", "24"], older.Headlines.Select(x => x.ArticleId));
	}

	[Fact]
	public void The_archive_is_searched_for_every_word_over_every_provider()
	{
		_archive.Manager.Add([
			Headline("PRN", "1", At(25), "Kimberly-Clark closes notes offering"),
			Headline("DJ-N", "2", At(26), "Kimberly-Clark raises guidance", source: "ibkr"),
			Headline("FED", "3", At(27), "FOMC statement"),
		]);

		var page = _archive.Manager.GetHeadlines(
			new NewsArchiveQueryDto { Text = "kimberly clark", Limit = 10 }
		);

		Assert.Equal(["2", "1"], page.Headlines.Select(x => x.ArticleId));
	}

	[Fact]
	public void Articles_read_once_are_kept_except_documents()
	{
		var headline = Headline("PRN", "1", At(29));
		_archive.Manager.SaveArticle(
			headline,
			new NewsArticleDto
			{
				Kind = NewsArticleKind.Html,
				Body = "<p>x</p>",
				Url = "https://x",
			}
		);
		var document = Headline("PRN", "2", At(29));
		_archive.Manager.SaveArticle(
			document,
			new NewsArticleDto { Kind = NewsArticleKind.Binary, Body = "JVBERi0=" }
		);

		var article = _archive.Manager.GetArticle(headline)!;

		Assert.Equal(
			(NewsArticleKind.Html, "<p>x</p>", "https://x"),
			(article.Kind, article.Body, article.Url)
		);
		Assert.Null(_archive.Manager.GetArticle(document));
	}

	[Fact]
	public void Retention_deletes_whole_days_past_the_setting_and_0_keeps_everything()
	{
		_archive.Manager.Add([Headline("PRN", "old", At(1)), Headline("PRN", "new", At(28))]);
		SaveRetention(0);
		_archive.Manager.ApplyRetention();
		Assert.Equal(2, _archive.Repository.GetDays().Count);

		SaveRetention(14);
		_archive.Manager.ApplyRetention();

		Assert.Equal([new DateOnly(2026, 10, 28)], _archive.Repository.GetDays());
		Assert.Equal(
			["new"],
			_archive
				.Manager.GetHeadlines(new NewsArchiveQueryDto { Limit = 10 })
				.Headlines.Select(x => x.ArticleId)
		);
	}

	[Fact]
	public void Routine_fund_filings_archived_earlier_are_not_shown()
	{
		_archive.Manager.Add([
			Headline(
				"SEC",
				"1",
				At(28),
				"SPY NPORT-P: Monthly Portfolio Investments Report",
				symbols: "SPY"
			),
			Headline("SEC", "2", At(29), "SPY 8-K: Other events", symbols: "SPY"),
		]);

		var page = _archive.Manager.GetHeadlines(
			new NewsArchiveQueryDto
			{
				ProviderKeys = Keys("lt:SEC"),
				Symbol = "SPY",
				Limit = 10,
			}
		);

		Assert.Equal(["2"], page.Headlines.Select(x => x.ArticleId));
	}

	[Fact]
	public void Clear_forgets_everything_so_it_can_be_archived_again()
	{
		_archive.Manager.Add([Headline("PRN", "1", At(29))]);

		_archive.Manager.Clear();

		Assert.Equal(0, _archive.Manager.GetSizeBytes());
		Assert.Single(_archive.Manager.Add([Headline("PRN", "1", At(29))]));
	}

	private void SaveRetention(int days)
	{
		var settings = _archive.SettingsManager.GetAppSettings();
		_archive.SettingsManager.SaveAppSettings(
			new SaveAppSettingsRequest
			{
				MarketDataType = settings.MarketDataType,
				HeartbeatSeconds = settings.HeartbeatSeconds,
				NewsRetentionDays = days,
			}
		);
	}
}
