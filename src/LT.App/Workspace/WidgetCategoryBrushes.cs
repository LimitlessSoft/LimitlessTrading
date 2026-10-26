using Avalonia.Media;
using LT.Contracts.Enums;

namespace LT.App.Workspace;

/// <summary>
/// The color of each widget category: the marker in the frame's top-left corner. Picked away
/// from the buy / sell green and red, the fullscreen frame's amber and the link group dots (a
/// deeper indigo than the Blue group, a muted gold darker than Yellow and Orange).
/// </summary>
public static class WidgetCategoryBrushes
{
	public static IBrush Ibkr { get; } = new SolidColorBrush(Color.Parse("#5C6BC0"));
	public static IBrush Tools { get; } = new SolidColorBrush(Color.Parse("#B8973A"));

	public static IBrush Get(WidgetCategory category) =>
		category switch
		{
			WidgetCategory.Tools => Tools,
			_ => Ibkr,
		};
}
