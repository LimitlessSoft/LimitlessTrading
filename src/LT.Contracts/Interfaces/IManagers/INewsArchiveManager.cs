using LT.Contracts.Dtos.News;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// Every headline the app has seen, from any source, kept on disk (<c>INewsArchiveRepository</c>)
/// so history works without the source (IB offline, a feed that only lists its latest items).
/// Each headline is stored once (by <c>NewsHelpers.GetKey</c>). Singleton: one key index.
/// </summary>
public interface INewsArchiveManager
{
	/// <summary>Headlines archived for the first time (never raised for one already there).</summary>
	event Action<IReadOnlyList<NewsHeadlineDto>>? HeadlinesAdded;

	/// <summary>Archives the headlines not archived yet and returns those; never throws.</summary>
	IReadOnlyList<NewsHeadlineDto> Add(IEnumerable<NewsHeadlineDto> headlines);

	/// <summary>Archived headlines, newest first; <see cref="NewsHeadlinesPageDto.HasMore"/> when older ones match.</summary>
	NewsHeadlinesPageDto GetHeadlines(NewsArchiveQueryDto query);

	/// <summary>The headline's article when it was read before; null otherwise.</summary>
	NewsArticleDto? GetArticle(NewsHeadlineDto headline);

	/// <summary>Keeps the article for offline reading (documents are not kept); never throws.</summary>
	void SaveArticle(NewsHeadlineDto headline, NewsArticleDto article);

	long GetSizeBytes();

	void Clear();

	/// <summary>Deletes the days older than the <c>NewsRetentionDays</c> setting (0: none).</summary>
	void ApplyRetention();
}
