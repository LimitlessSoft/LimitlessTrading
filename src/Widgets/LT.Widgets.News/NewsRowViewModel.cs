using System.Globalization;
using LT.Contracts.Dtos.News;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.News;

/// <summary>One headline in the list: time · provider · headline · sentiment.</summary>
public class NewsRowViewModel(NewsHeadlineDto headline)
{
	// Scores this close to 0 are shown as neutral
	private const double NeutralSentiment = 0.05;

	public NewsHeadlineDto Headline { get; } = headline;

	public string Key { get; } = NewsHelpers.GetKey(headline);

	public DateTimeOffset Time => Headline.Time;

	/// <summary>Local time; the date only when it isn't today.</summary>
	public string TimeText
	{
		get
		{
			var local = Headline.Time.ToLocalTime();
			return local.Date == DateTime.Today
				? local.ToString("HH:mm", CultureInfo.CurrentCulture)
				: local.ToString("MMM dd HH:mm", CultureInfo.CurrentCulture);
		}
	}

	public string Provider => Headline.ProviderCode;

	public string Text => Headline.Headline;

	public bool HasSentiment => Headline.Sentiment != null;

	public bool IsPositive => Headline.Sentiment > NeutralSentiment;

	public bool IsNegative => Headline.Sentiment < -NeutralSentiment;

	public string SentimentMarker =>
		IsPositive ? "▲"
		: IsNegative ? "▼"
		: "●";

	public string? SentimentTip =>
		Headline.Sentiment is { } sentiment
			? Headline.Confidence is { } confidence
				? $"Sentiment {sentiment.ToString("+0.00;-0.00;0.00", CultureInfo.CurrentCulture)}, confidence {confidence.ToString("0.00", CultureInfo.CurrentCulture)}"
				: $"Sentiment {sentiment.ToString("+0.00;-0.00;0.00", CultureInfo.CurrentCulture)}"
			: null;
}
