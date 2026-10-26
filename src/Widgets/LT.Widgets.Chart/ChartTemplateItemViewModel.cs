using CommunityToolkit.Mvvm.ComponentModel;
using LT.Contracts.Dtos.ChartTemplates;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

/// <summary>
/// One row of the chart's template menu: apply it, make it the default for new charts, rename it
/// in place, export it or delete it.
/// </summary>
public partial class ChartTemplateItemViewModel(ChartViewModel owner, ChartTemplateDto template)
	: ObservableObject
{
	/// <summary>The chart: the row's buttons run its commands (the menu lives in a popup).</summary>
	public ChartViewModel Owner => owner;
	public string Id => template.Id;
	public string Name => template.Name;
	public bool IsDefault => template.IsDefault;

	public string DefaultToolTip =>
		template.IsDefault ? "Applied to new charts — click to stop" : "Apply to every new chart";

	/// <summary>What it sets: "5m · 2 indicators".</summary>
	public string Summary
	{
		get
		{
			var parts = new List<string>();
			if (template.Settings.Timeframe is { } timeframe)
				parts.Add(ChartTimeframeHelpers.GetDisplayName(timeframe));
			if (template.Settings.Indicators is { } indicators)
				parts.Add(
					indicators.Count switch
					{
						0 => "no indicators",
						1 => "1 indicator",
						var n => $"{n} indicators",
					}
				);
			return string.Join(" · ", parts);
		}
	}

	public string DeletePrompt => $"Delete {template.Name}?";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsIdle))]
	public partial bool IsRenaming { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsIdle))]
	public partial bool IsConfirmingDelete { get; set; }

	/// <summary>Neither renaming nor confirming: shows the name and the row's buttons.</summary>
	public bool IsIdle => !IsRenaming && !IsConfirmingDelete;

	[ObservableProperty]
	public partial string RenameText { get; set; } = template.Name;
}
