using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

public static class WidgetMenuHelpers
{
	/// <summary>Section header of a category in menu → Widgets; the frame's corner marker tooltip.</summary>
	public static string GetCategoryTitle(WidgetCategory category) =>
		category switch
		{
			WidgetCategory.Ibkr => "Interactive Brokers",
			WidgetCategory.Tools => "Tools",
			WidgetCategory.Mixed => "Interactive Brokers + own sources",
			_ => category.ToString(),
		};

	/// <summary>
	/// The menu section a category's widgets are listed in: a mixed widget under Interactive
	/// Brokers, the rest under their own.
	/// </summary>
	public static WidgetCategory GetMenuSection(WidgetCategory category) =>
		category == WidgetCategory.Mixed ? WidgetCategory.Ibkr : category;

	/// <summary>
	/// Menu → Widgets in sections: <paramref name="order"/> (null is a separator) split by
	/// <paramref name="categoryOf"/>, one section per category in the enum's order, each keeping the
	/// menu order and the separators between its own widgets. Separators left at either end of a
	/// section, or doubled, are dropped; a category without widgets gets no section.
	/// </summary>
	public static IReadOnlyList<(
		WidgetCategory Category,
		IReadOnlyList<WorkspaceTool?> Items
	)> Group(IReadOnlyList<WorkspaceTool?> order, Func<WorkspaceTool, WidgetCategory> categoryOf)
	{
		var sections = new List<(WidgetCategory, IReadOnlyList<WorkspaceTool?>)>();
		foreach (var category in Enum.GetValues<WidgetCategory>())
		{
			var items = new List<WorkspaceTool?>();
			var separate = false;
			foreach (var entry in order)
			{
				if (entry is not { } tool)
				{
					separate = items.Count > 0;
					continue;
				}
				if (categoryOf(tool) != category)
					continue;
				if (separate)
					items.Add(null);
				separate = false;
				items.Add(tool);
			}
			if (items.Count > 0)
				sections.Add((category, items));
		}
		return sections;
	}
}
