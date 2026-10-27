using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Stores;

/// <summary>
/// Privacy mode (the eye in the main window's title bar): hides account ids, balances, P&amp;L and
/// quantities everywhere behind <see cref="Sensitive.Mask"/>. Saved, so it survives a restart.
/// Set on the UI thread.
/// </summary>
public partial class PrivacyStore : ObservableObject
{
	private readonly ISettingsManager _settingsManager;

	public PrivacyStore(ISettingsManager settingsManager)
	{
		_settingsManager = settingsManager;
		IsSensitiveHidden = settingsManager.GetHideSensitiveValues();
		Sensitive.IsHidden = IsSensitiveHidden;
	}

	[ObservableProperty]
	public partial bool IsSensitiveHidden { get; set; }

	partial void OnIsSensitiveHiddenChanged(bool value)
	{
		Sensitive.IsHidden = value;
		_settingsManager.SaveHideSensitiveValues(value);
	}
}
