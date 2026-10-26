using LT.Contracts.Dtos.ChartTemplates;

namespace LT.Contracts.Requests.ChartTemplates;

/// <summary>Replaces the settings of a template (found by id), keeping its name and default.</summary>
public class UpdateChartTemplateSettingsRequest
{
	public string Id { get; set; } = string.Empty;
	public ChartTemplateSettingsDto Settings { get; set; } = new();
}
