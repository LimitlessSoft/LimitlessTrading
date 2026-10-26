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

	/// <summary>False while it doesn't apply (it depends on another setting); shown greyed out.</summary>
	[ObservableProperty]
	public partial bool IsEnabled { get; set; } = true;

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

/// <summary>
/// A line of text; a secret one (a token) is masked. <c>normalize</c> cleans what is typed or
/// pasted (e.g. pulls the query id out of a pasted URL) as soon as it changes.
/// </summary>
public partial class TextSettingViewModel(
	string title,
	bool isSecret = false,
	string? placeholder = null,
	string? description = null,
	string? keywords = null,
	Func<string, string>? normalize = null
) : SettingItemViewModel(title, description, keywords)
{
	public bool IsSecret { get; } = isSecret;
	public char PasswordChar => IsSecret ? '•' : '\0';
	public string? Placeholder { get; } = placeholder;

	[ObservableProperty]
	public partial string Value { get; set; } = string.Empty;

	partial void OnValueChanged(string value)
	{
		if (normalize?.Invoke(value) is { } clean && clean != value)
			Value = clean;
	}
}

/// <summary>A value of a setup step with a Copy button.</summary>
public record GuideValueViewModel(string Label, string Value);

/// <summary>One numbered step of a guide: what to do, values to copy, optionally a page to open.</summary>
public record GuideStepViewModel(
	int Number,
	string Text,
	IReadOnlyList<GuideValueViewModel> Values,
	string? LinkText = null,
	string? Url = null
)
{
	public bool HasValues => Values.Count > 0;
	public bool HasLink => Url != null;
}

/// <summary>A numbered checklist (IB Flex setup); the window copies values and opens links.</summary>
public class GuideSettingViewModel(
	string title,
	IReadOnlyList<GuideStepViewModel> steps,
	string? description = null,
	string? keywords = null
) : SettingItemViewModel(title, description, keywords)
{
	public IReadOnlyList<GuideStepViewModel> Steps { get; } = steps;
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

	/// <summary>Work started by the button is running: a progress bar shows, the button waits.</summary>
	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(RunCommand))]
	public partial bool IsBusy { get; set; }

	[RelayCommand(CanExecute = nameof(CanRun))]
	private void Run() => action();

	private bool CanRun() => !IsBusy;
}
