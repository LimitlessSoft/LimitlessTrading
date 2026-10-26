using CommunityToolkit.Mvvm.ComponentModel;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Settings;

/// <summary>One setting on a tab: its label, help text and words search also finds it by.</summary>
public abstract partial class SettingItemViewModel(
	string title,
	string? description = null,
	string? keywords = null
) : ObservableObject
{
	public string Title { get; } = title;
	public string? Description { get; } = description;
	public bool HasDescription => !string.IsNullOrEmpty(Description);

	/// <summary>False while a search runs that this setting doesn't match.</summary>
	[ObservableProperty]
	public partial bool IsVisible { get; set; } = true;

	public bool Matches(string? query) =>
		SettingsSearchHelpers.Matches(query, Title, Description, keywords);
}

/// <summary>An on / off setting (a check box).</summary>
public partial class BoolSettingViewModel(
	string title,
	string? description = null,
	string? keywords = null
) : SettingItemViewModel(title, description, keywords)
{
	[ObservableProperty]
	public partial bool Value { get; set; }
}

/// <summary>One of a fixed set of values (a drop-down).</summary>
public partial class ChoiceSettingViewModel(
	string title,
	IReadOnlyList<object> options,
	string? description = null,
	string? keywords = null
) : SettingItemViewModel(title, description, keywords)
{
	public IReadOnlyList<object> Options { get; } = options;

	[ObservableProperty]
	public partial object? Value { get; set; }
}

/// <summary>A whole number within limits (a spin box).</summary>
public partial class NumberSettingViewModel(
	string title,
	decimal minimum,
	decimal maximum,
	string? description = null,
	string? keywords = null
) : SettingItemViewModel(title, description, keywords)
{
	public decimal Minimum { get; } = minimum;
	public decimal Maximum { get; } = maximum;

	[ObservableProperty]
	public partial decimal? Value { get; set; }
}
