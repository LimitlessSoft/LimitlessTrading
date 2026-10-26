using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Enums;

namespace LT.App.Widgets.Chart;

/// <summary>A row of the chart's indicator picker.</summary>
public class IndicatorChoiceItem(IndicatorDefinitionDto definition)
{
	public string Id => definition.Id;
	public string Name => definition.Name;

	public string Kind =>
		definition.Source == IndicatorSource.BuiltIn ? "Built-in"
		: definition.IsCompiling ? "Script · compiling…"
		: "Script";

	public string? Error => definition.Error;
	public bool HasError => definition.Error != null;
}
