using Avalonia.Controls;
using Avalonia.Data;

namespace LT.App.Workspace;

public static class WidgetTitles
{
	/// <summary>Plain header title text bound to the view model, e.g. the option chain's ticker.</summary>
	public static TextBlock Bound(string property) =>
		new() { Classes = { "panelTitle" }, [!TextBlock.TextProperty] = new Binding(property) };
}
