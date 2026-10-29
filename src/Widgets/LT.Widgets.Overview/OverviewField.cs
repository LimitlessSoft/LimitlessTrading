using CommunityToolkit.Mvvm.ComponentModel;

namespace LT.App.Widgets.Overview;

/// <summary>One labelled value of an overview section, e.g. "Exchange · ARCA".</summary>
public partial class OverviewField(string label, string value, string? toolTip = null)
	: ObservableObject
{
	public string Label { get; } = label;

	[ObservableProperty]
	public partial string Value { get; set; } = value;

	/// <summary>More than fits the row (every routing exchange, where a number comes from).</summary>
	[ObservableProperty]
	public partial string? ToolTip { get; set; } = toolTip;
}

/// <summary>A titled group of fields ("Instrument", "Trading" …).</summary>
public class OverviewSection(string title, IReadOnlyList<OverviewField> fields)
{
	public string Title { get; } = title;

	public IReadOnlyList<OverviewField> Fields { get; } = fields;
}
