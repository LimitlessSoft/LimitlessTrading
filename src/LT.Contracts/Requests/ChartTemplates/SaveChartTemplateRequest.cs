using LT.Contracts.Dtos.ChartTemplates;

namespace LT.Contracts.Requests.ChartTemplates;

/// <summary>Saves a chart's setup under a name.</summary>
public class SaveChartTemplateRequest
{
	public string Name { get; set; } = string.Empty;
	public ChartTemplateSettingsDto Settings { get; set; } = new();

	/// <summary>Replace the settings of a template with this name (keeping its id and default).</summary>
	public bool Overwrite { get; set; }
}
