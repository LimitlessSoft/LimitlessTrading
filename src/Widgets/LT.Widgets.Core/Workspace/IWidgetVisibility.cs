namespace LT.App.Workspace;

/// <summary>
/// A widget view model told by its workspace whether it is on screen: in the shown center view,
/// expanded in its side or bottom panel, fullscreen, or floating in a window that isn't
/// minimized — and the workspace's own window isn't minimized. A hidden widget lets go of its
/// market data streams and keeps everything else, so showing it again only waits for fresh quotes.
/// A view model never told anything (e.g. the order ticket in a dialog) counts as shown.
/// </summary>
public interface IWidgetVisibility
{
	/// <summary>Called on the UI thread whenever the widget appears or disappears.</summary>
	void SetShown(bool isShown);
}
