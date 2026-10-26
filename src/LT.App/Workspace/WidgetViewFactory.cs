using Avalonia.Controls;

namespace LT.App.Workspace;

/// <summary>Creates the view of a widget; each view is created once and moved between hosts.</summary>
public static class WidgetViewFactory
{
	public static Control Create(WidgetItem widget)
	{
		var view = WidgetCatalog.Get(widget.Tool).CreateView();
		view.DataContext = widget.Content;
		return view;
	}

	/// <summary>A header title of the widget's own, or null for the plain title text.</summary>
	public static Control? CreateTitle(WidgetItem widget)
	{
		if (WidgetCatalog.Get(widget.Tool).CreateTitle() is not { } title)
			return null;
		title.DataContext = widget.Content;
		return title;
	}
}
