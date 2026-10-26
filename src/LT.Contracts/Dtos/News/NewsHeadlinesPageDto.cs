namespace LT.Contracts.Dtos.News;

/// <summary>Past headlines, newest first.</summary>
public class NewsHeadlinesPageDto
{
	public IReadOnlyList<NewsHeadlineDto> Headlines { get; set; } = [];

	/// <summary>Older headlines exist (page back with the oldest one's time).</summary>
	public bool HasMore { get; set; }
}
