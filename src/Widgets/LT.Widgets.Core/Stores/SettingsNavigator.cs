namespace LT.App.Stores;

/// <summary>
/// Lets a widget open the Settings window on a page (a setup banner's "Set up…"); the main window
/// listens to <see cref="OpenRequested"/> and opens it. Singleton (Program.cs).
/// </summary>
public class SettingsNavigator
{
	/// <summary>Raised on the caller's thread with the page title to show (null: the default page).</summary>
	public event Action<string?>? OpenRequested;

	public void Open(string? pageTitle = null) => OpenRequested?.Invoke(pageTitle);
}
