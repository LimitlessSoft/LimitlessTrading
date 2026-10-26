using LT.Contracts.Dtos.ChartTemplates;

namespace LT.Contracts.Requests.ChartTemplates;

/// <summary>The templates read from an import file, checked before any is added.</summary>
public class ImportChartTemplatesRequest
{
	public string? Format { get; set; }
	public IReadOnlyList<ChartTemplateDto> Templates { get; set; } = [];
}
