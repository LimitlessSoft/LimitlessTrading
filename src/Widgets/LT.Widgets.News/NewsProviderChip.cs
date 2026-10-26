using CommunityToolkit.Mvvm.ComponentModel;
using LT.Contracts.Dtos.News;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.News;

/// <summary>One provider in the filter row; selected ones are asked for headlines.</summary>
public partial class NewsProviderChip(NewsProviderDto provider) : ObservableObject
{
	public NewsProviderDto Provider { get; } = provider;

	public string Key { get; } = NewsHelpers.GetProviderKey(provider);

	public string Label => Provider.Code;

	public string Tip => $"{Provider.Name} ({Provider.Code})";

	[ObservableProperty]
	public partial bool IsSelected { get; set; } = true;

	/// <summary>Null until checked (in All news mode); false dims the chip there.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasNoBroadTape))]
	public partial bool? HasBroadTape { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasNoBroadTape))]
	public partial bool InAllMode { get; set; }

	/// <summary>Dimmed: All news mode and the provider has no broad tape.</summary>
	public bool HasNoBroadTape => InAllMode && HasBroadTape == false;
}
