using CommunityToolkit.Mvvm.ComponentModel;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Overview;

/// <summary>One labelled value of an overview section, e.g. "Exchange · ARCA".</summary>
public partial class OverviewField(
	string label,
	string value,
	string? toolTip = null,
	string? description = null
) : ObservableObject
{
	public string Label { get; } = label;

	/// <summary>What the value means ("The highest price a buyer is bidding …").</summary>
	public string? Description { get; } = description;

	[ObservableProperty]
	public partial string Value { get; set; } = value;

	/// <summary>More than fits the row (every routing exchange, where a number comes from).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Tip))]
	public partial string? ToolTip { get; set; } = toolTip;

	/// <summary>What the row shows on hover: the meaning, then the details.</summary>
	public string? Tip => OverviewHelpers.JoinTip(Description, ToolTip);
}

/// <summary>A titled group of fields ("Instrument", "Trading" …).</summary>
public class OverviewSection(string title, IReadOnlyList<OverviewField> fields)
{
	public string Title { get; } = title;

	public IReadOnlyList<OverviewField> Fields { get; } = fields;
}
