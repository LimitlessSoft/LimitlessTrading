using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Settings;

namespace LT.App.Stores;

/// <summary>
/// The app-wide UI scale (zoom): the status bar picker and Settings → Appearance both set
/// <see cref="Percent"/>, which is saved and pushed into <see cref="UiScale.Factor"/> at once, so
/// every window, widget and popup follows without a restart. Resolved before the main window is
/// created, so the first frame is already drawn at the saved scale. Set on the UI thread.
/// </summary>
public partial class UiScaleStore : ObservableObject
{
	private readonly ISettingsManager _settingsManager;
	private readonly bool _loaded;

	public UiScaleStore(ISettingsManager settingsManager)
	{
		_settingsManager = settingsManager;
		var percent = settingsManager.GetUiScalePercent();
		Options = [.. UiScaleHelpers.Options(percent).Select(x => new UiScaleOption(x))];
		Percent = percent;
		_loaded = true;
	}

	/// <summary>The pickers' choices (the steps, and a saved value that is none of them).</summary>
	public IReadOnlyList<UiScaleOption> Options { get; }

	/// <summary>Percent of the normal size.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Selected))]
	public partial int Percent { get; set; }

	/// <summary><see cref="Percent"/> as one of <see cref="Options"/>, for a picker.</summary>
	public UiScaleOption? Selected
	{
		get => Options.FirstOrDefault(x => x.Percent == Percent);
		set
		{
			if (value != null)
				Percent = value.Percent;
		}
	}

	partial void OnPercentChanged(int value)
	{
		// Saved first: a value the manager refuses is never applied
		if (_loaded)
			_settingsManager.SaveUiScale(new SaveUiScaleRequest { Percent = value });
		UiScale.Factor = value / 100.0;
	}
}
