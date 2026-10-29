using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Stores;
using LT.Contracts.Dtos.News;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.News;

/// <summary>One headline in the list: time · provider · headline · sentiment; matches marked.</summary>
public class NewsRowViewModel(NewsHeadlineDto headline, DisplayTimeStore displayTime, bool isMatch)
	: ObservableObject
{
	// Scores this close to 0 are shown as neutral
	private const double NeutralSentiment = 0.05;

	public NewsHeadlineDto Headline { get; } = headline;

	public string Key { get; } = NewsHelpers.GetKey(headline);

	public DateTimeOffset Time => Headline.Time;

	/// <summary>In the display time zone; the date only when it isn't today, the year only when it isn't this year.</summary>
	public string TimeText =>
		displayTime.FormatDayTime(Headline.Time, "HH:mm", "MMM dd HH:mm", "MMM dd yyyy HH:mm");

	/// <summary>The display time zone changed.</summary>
	public void RefreshTime() => OnPropertyChanged(nameof(TimeText));

	public string Provider => Headline.ProviderCode;

	public string Text => Headline.Headline;

	/// <summary>It matches the news alert rules (a keyword, a watchlist symbol): marked in the list.</summary>
	public bool IsMatch { get; } = isMatch;

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
