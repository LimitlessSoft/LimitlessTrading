using Avalonia.Controls;
using Avalonia.Input;
using LT.Contracts.Enums;

namespace LT.App.Workspace;

/// <summary>
/// Everything the workspace needs to host a widget that lives in its own project: catalog entry
/// (title, icon, focus shortcut), its view model type (resolved from DI) and its views. Listed in
/// <c>WidgetCatalog.Definitions</c>.
/// </summary>
public interface IWidgetDefinition
{
	WorkspaceTool Tool { get; }

	string Title { get; }

	/// <summary>Path data on a 24×24 grid (even-odd fill, so inner shapes cut out).</summary>
	string Icon { get; }

	/// <summary>Ctrl+Alt+key that shows and focuses the widget; null for none.</summary>
	Key? FocusKey { get; }

	/// <summary>Registered as transient; one is created per spawned instance.</summary>
	Type ViewModelType { get; }

	/// <summary>The widget's body; the workspace sets its DataContext to the view model.</summary>
	Control CreateView();

	/// <summary>A header title of the widget's own, or null for the plain title text.</summary>
	Control? CreateTitle();
}
