namespace LT.Contracts.Dtos.ChartTemplates;

/// <summary>A saved chart template.</summary>
public class ChartTemplateDto
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public int Version { get; set; }

	/// <summary>Applied to every new chart.</summary>
	public bool IsDefault { get; set; }

	public ChartTemplateSettingsDto Settings { get; set; } = new();
}
