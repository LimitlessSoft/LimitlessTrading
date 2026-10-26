namespace LT.Contracts.Entities;

/// <summary>An exported file of chart templates (one or several).</summary>
public class ChartTemplateFileEntity
{
	/// <summary><c>ChartTemplateConstants.FileFormat</c>; anything else is not a template file.</summary>
	public string? Format { get; set; }

	public int Version { get; set; }
	public List<ChartTemplateEntity>? Templates { get; set; }
}
