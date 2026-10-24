using Avalonia.Controls;

namespace LT.App.Workspace;

/// <summary>A window that can show one widget frame over all of its content (fullscreen widget).</summary>
public interface IFullscreenHost
{
	/// <summary>Shows <paramref name="frame"/> over everything; null brings the normal content back.</summary>
	void ShowFullscreen(Control? frame);
}
