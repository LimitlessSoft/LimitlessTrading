using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Settings;

/// <summary>A tab of a settings page and the settings on it.</summary>
public partial class SettingsTabViewModel(string title, params SettingItemViewModel[] items)
	: ObservableObject
{
	public string Title { get; } = title;
	public IReadOnlyList<SettingItemViewModel> Items { get; } = items;

	/// <summary>False while a search runs that none of its settings match.</summary>
	[ObservableProperty]
	public partial bool IsVisible { get; set; } = true;
}

/// <summary>
/// A node of the settings tree. A page shows its tabs on the right (the tab strip only when it has
/// more than one); a group (no tabs) only holds child pages and opens its first one.
/// </summary>
public partial class SettingsPageViewModel : ObservableObject
{
	public SettingsPageViewModel(
		string title,
		IReadOnlyList<SettingsTabViewModel>? tabs = null,
		IReadOnlyList<SettingsPageViewModel>? children = null
	)
	{
		Title = title;
		Tabs = tabs ?? [];
		Children = children ?? [];
		foreach (var child in Children)
			child.Parent = this;
		Filter(null);
	}

	public string Title { get; }
	public IReadOnlyList<SettingsTabViewModel> Tabs { get; }
	public IReadOnlyList<SettingsPageViewModel> Children { get; }
	public SettingsPageViewModel? Parent { get; private set; }

	public bool IsGroup => Tabs.Count == 0;

	/// <summary>"General › Updates": the page header.</summary>
	public string Path => Parent is { } parent ? $"{parent.Path} › {Title}" : Title;

	/// <summary>Tree children left by the current search.</summary>
	public ObservableCollection<SettingsPageViewModel> VisibleChildren { get; } = [];

	/// <summary>Tabs left by the current search; the strip shows when there are several.</summary>
	public ObservableCollection<SettingsTabViewModel> VisibleTabs { get; } = [];

	public bool HasTabStrip => VisibleTabs.Count > 1;

	[ObservableProperty]
	public partial SettingsTabViewModel? SelectedTab { get; set; }

	[ObservableProperty]
	public partial bool IsExpanded { get; set; } = true;

	/// <summary>
	/// Applies a search to this page and its children; returns whether anything here matches. An
	/// empty query shows everything; a page, group or tab whose own title matches shows all of its
	/// settings.
	/// </summary>
	public bool Filter(string? query, bool showAll = false)
	{
		showAll |= TitleMatches(query, Title);
		VisibleTabs.Clear();
		foreach (var tab in Tabs)
		{
			var showTab = showAll || TitleMatches(query, tab.Title);
			foreach (var item in tab.Items)
				item.IsVisible = showTab || item.Matches(query);
			tab.IsVisible = tab.Items.Any(x => x.IsVisible);
			if (tab.IsVisible)
				VisibleTabs.Add(tab);
		}
		if (SelectedTab is null || !VisibleTabs.Contains(SelectedTab))
			SelectedTab = VisibleTabs.FirstOrDefault();
		OnPropertyChanged(nameof(HasTabStrip));

		VisibleChildren.Clear();
		foreach (var child in Children)
			if (child.Filter(query, showAll))
				VisibleChildren.Add(child);
		return VisibleTabs.Count > 0 || VisibleChildren.Count > 0;
	}

	private static bool TitleMatches(string? query, string title) =>
		!string.IsNullOrWhiteSpace(query) && SettingsSearchHelpers.Matches(query, title);

	/// <summary>The page to show for this node: itself, or a group's first visible page.</summary>
	public SettingsPageViewModel? FirstPage =>
		IsGroup ? VisibleChildren.Select(x => x.FirstPage).FirstOrDefault(x => x != null) : this;
}
