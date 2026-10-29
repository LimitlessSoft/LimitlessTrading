using CommunityToolkit.Mvvm.ComponentModel;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.News;

/// <summary>One provider in the filter row; selected ones are asked for headlines.</summary>
public partial class NewsProviderChip(NewsProviderDto provider) : ObservableObject
{
	public NewsProviderDto Provider { get; } = provider;

	public string Key { get; } = NewsHelpers.GetProviderKey(provider);

	public string Label => Provider.Code;

	public string Tip =>
		Provider.SourceId == FeedNewsConstants.SourceId
			? $"{Provider.Name} ({Provider.Code}) · {FeedNewsConstants.SourceName} feed"
			: $"{Provider.Name} ({Provider.Code}) · IB";

	[ObservableProperty]
	public partial bool IsSelected { get; set; } = true;

	/// <summary>Null until checked (in All news mode); false dims the chip there.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsDimmed))]
	public partial bool? HasBroadTape { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsDimmed))]
	public partial NewsMode Mode { get; set; }

	/// <summary>
	/// Dimmed where it has nothing to give: All news without a broad feed, Linked without news
	/// per symbol (the Fed, CNBC …).
	/// </summary>
	public bool IsDimmed =>
		Mode switch
		{
			NewsMode.All => HasBroadTape == false
				|| !Provider.Capabilities.HasFlag(NewsSourceCapabilities.BroadTape),
			NewsMode.Linked => !Provider.Capabilities.HasFlag(NewsSourceCapabilities.BySymbol),
			_ => false,
		};
}
