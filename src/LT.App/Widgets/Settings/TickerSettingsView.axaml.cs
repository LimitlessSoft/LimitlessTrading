using Avalonia.Controls;

namespace LT.App.Widgets.Settings;

/// <summary>
/// Drives the ticker box of <see cref="TickerSettingsSettingViewModel"/>: a picked listing or an
/// entered ticker is added, the box empties after each.
/// </summary>
public partial class TickerSettingsView : UserControl
{
	public TickerSettingsView()
	{
		InitializeComponent();
		TickerBox.Suggest = (text, ct) =>
			ViewModel?.GetSuggestionsAsync(text, ct) ?? Task.FromResult(Enumerable.Empty<object>());
		TickerBox.Picked += item =>
		{
			TickerBox.Reset(ViewModel?.GetRecentSuggestions());
			ViewModel?.Add(item.Contract);
		};
		TickerBox.Entered += text =>
		{
			TickerBox.Reset(ViewModel?.GetRecentSuggestions());
			_ = ViewModel?.AddTickerAsync(text);
		};
		TickerBox.Cancelled += () => TickerBox.Reset(ViewModel?.GetRecentSuggestions());
		// An empty box lists the recent contracts (an option charted before, too)
		AttachedToVisualTree += (_, _) => TickerBox.Reset(ViewModel?.GetRecentSuggestions());
	}

	private TickerSettingsSettingViewModel? ViewModel =>
		DataContext as TickerSettingsSettingViewModel;
}
