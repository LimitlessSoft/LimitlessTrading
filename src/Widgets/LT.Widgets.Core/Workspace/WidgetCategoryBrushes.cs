using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using LT.Contracts.Enums;

namespace LT.App.Workspace;

/// <summary>
/// The color of each widget category: the marker in the frame's top-left corner, and wherever a
/// widget tells IB's items from the app's own (the News badges). Picked away from the buy / sell
/// green and red, the fullscreen frame's amber and the link group dots (a deeper indigo than the
/// Blue group, a muted gold darker than Yellow and Orange).
/// </summary>
public static class WidgetCategoryBrushes
{
	private static readonly Color IbkrColor = Color.Parse("#5C6BC0");
	private static readonly Color ToolsColor = Color.Parse("#B8973A");

	public static IBrush Ibkr { get; } = new SolidColorBrush(IbkrColor);
	public static IBrush Tools { get; } = new SolidColorBrush(ToolsColor);

	/// <summary>
	/// Both colors, split hard along the diagonal from the top-left corner: on the corner wedge,
	/// the top half IB's, the left half the tools'.
	/// </summary>
	public static IBrush Mixed { get; } =
		new ImmutableLinearGradientBrush(
			[
				new ImmutableGradientStop(0, ToolsColor),
				new ImmutableGradientStop(0.5, ToolsColor),
				new ImmutableGradientStop(0.5, IbkrColor),
				new ImmutableGradientStop(1, IbkrColor),
			],
			startPoint: new RelativePoint(0, 1, RelativeUnit.Relative),
			endPoint: new RelativePoint(1, 0, RelativeUnit.Relative)
		);

	public static IBrush Get(WidgetCategory category) =>
		category switch
		{
			WidgetCategory.Tools => Tools,
			WidgetCategory.Mixed => Mixed,
			_ => Ibkr,
		};
}
