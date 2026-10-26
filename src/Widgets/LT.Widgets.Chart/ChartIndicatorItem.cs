namespace LT.App.Widgets.Chart;

/// <summary>A row of the indicator manager's "on this chart" list.</summary>
public class ChartIndicatorItem(string instanceId, string label, bool isVisible)
{
	public string InstanceId => instanceId;

	/// <summary>The legend text ("BB 20 2").</summary>
	public string Label => label;

	public bool IsVisible => isVisible;
}
