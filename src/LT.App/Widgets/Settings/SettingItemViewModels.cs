using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

/// <summary>A line of text; a secret one (a token) is masked.</summary>
public partial class TextSettingViewModel(
	string title,
	bool isSecret = false,
	string? placeholder = null,
	string? description = null,
	string? keywords = null
) : SettingItemViewModel(title, description, keywords)
{
	public bool IsSecret { get; } = isSecret;
	public char PasswordChar => IsSecret ? '•' : '\0';
	public string? Placeholder { get; } = placeholder;

	[ObservableProperty]
	public partial string Value { get; set; } = string.Empty;
}

/// <summary>A paragraph of guidance (no value), like setup steps.</summary>
public class NoteSettingViewModel(string title, string text, string? keywords = null)
	: SettingItemViewModel(title, text, keywords);

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

/// <summary>A read-out with a button that acts right away (not on Save), like Clear cache.</summary>
public partial class ActionSettingViewModel(
	string title,
	string buttonText,
	Action action,
	string? description = null,
	string? keywords = null
) : SettingItemViewModel(title, description, keywords)
{
	public string ButtonText { get; } = buttonText;

	/// <summary>The value shown next to the title, e.g. the cache's size.</summary>
	[ObservableProperty]
	public partial string? Status { get; set; }

	[RelayCommand]
	private void Run() => action();
}
