using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Stores;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Overview;

/// <summary>One headline of the News tab: time · provider badge · headline.</summary>
public class NewsTabRow(NewsHeadlineDto headline, DisplayTimeStore displayTime) : ObservableObject
{
	public NewsHeadlineDto Headline { get; } = headline;

	public string Key { get; } = NewsHelpers.GetKey(headline);

	/// <summary>In the display time zone; the date only when it isn't today, the year only when it isn't this year.</summary>
	public string TimeText =>
		displayTime.FormatDayTime(Headline.Time, "HH:mm", "MMM dd HH:mm", "MMM dd yyyy HH:mm");

	/// <summary>The display time zone changed.</summary>
	public void RefreshTime() => OnPropertyChanged(nameof(TimeText));

	public string Provider => Headline.ProviderCode;

	/// <summary>IB's or the app's own feeds: the badge's color, as in the News widget.</summary>
	public IBrush SourceBrush =>
		WidgetCategoryBrushes.Get(NewsHelpers.GetSourceCategory(Headline.SourceId));

	public string SourceTip =>
		NewsHelpers.GetSourceCategory(Headline.SourceId) == WidgetCategory.Tools
			? $"{Headline.ProviderCode} · {FeedNewsConstants.SourceName} feed"
			: $"{Headline.ProviderCode} · IB";

	public string Text => Headline.Headline;
}
