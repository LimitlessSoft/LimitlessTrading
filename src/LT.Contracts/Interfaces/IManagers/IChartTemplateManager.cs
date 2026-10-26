using LT.Contracts.Dtos.ChartTemplates;
using LT.Contracts.Requests.ChartTemplates;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// Chart templates: named chart setups (timeframe, indicators, …) saved in settings, applied from
/// any chart's header, optionally one applied to every new chart, exported to and imported from
/// JSON files.
/// </summary>
public interface IChartTemplateManager
{
	/// <summary>By name.</summary>
	IReadOnlyList<ChartTemplateDto> GetTemplates();

	ChartTemplateDto? GetTemplate(string id);

	/// <summary>The template with this name (case-insensitive, trimmed); null when none has it.</summary>
	ChartTemplateDto? FindByName(string name);

	/// <summary>The template applied to new charts; null when none is set (or it was deleted).</summary>
	ChartTemplateDto? GetDefault();

	/// <summary>
	/// Saves the settings under the name. A taken name is refused unless
	/// <see cref="SaveChartTemplateRequest.Overwrite"/>, which replaces that template's settings.
	/// </summary>
	ChartTemplateDto Save(SaveChartTemplateRequest request);

	ChartTemplateDto Rename(RenameChartTemplateRequest request);

	/// <summary>Deletes the template (and unsets it as the default).</summary>
	void Delete(string id);

	/// <summary>The template applied to new charts; null: none.</summary>
	void SetDefault(string? id);

	/// <summary>A JSON file of the templates (null: all of them).</summary>
	string Export(IReadOnlyCollection<string>? ids = null);

	/// <summary>
	/// Adds the templates of an exported file. The whole file is checked first (nothing is added
	/// when any template is invalid); a name already taken gets " (2)", " (3)" … Returns the added
	/// templates.
	/// </summary>
	IReadOnlyList<ChartTemplateDto> Import(string json);
}
