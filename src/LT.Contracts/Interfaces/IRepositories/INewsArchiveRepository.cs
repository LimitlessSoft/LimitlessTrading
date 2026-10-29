using LT.Contracts.Entities;

namespace LT.Contracts.Interfaces.IRepositories;

/// <summary>
/// The local news archive: one append-only JSON-lines file per UTC day of headlines, and one of
/// articles. A line that can't be read is skipped, never fatal.
/// </summary>
public interface INewsArchiveRepository
{
	/// <summary>Days with a headline file, oldest first.</summary>
	IReadOnlyList<DateOnly> GetDays();

	/// <summary>The day's headlines in the order they were written.</summary>
	IReadOnlyList<NewsHeadlineEntity> GetHeadlines(DateOnly day);

	void AppendHeadlines(DateOnly day, IReadOnlyList<NewsHeadlineEntity> headlines);

	/// <summary>Articles of the day's headlines, in the order they were written.</summary>
	IReadOnlyList<NewsArticleEntity> GetArticles(DateOnly day);

	void AppendArticle(DateOnly day, NewsArticleEntity article);

	/// <summary>Deletes the day's headlines and articles.</summary>
	void DeleteDay(DateOnly day);

	/// <summary>Bytes of every archive file.</summary>
	long GetSizeBytes();

	/// <summary>Deletes everything archived.</summary>
	void Clear();
}
