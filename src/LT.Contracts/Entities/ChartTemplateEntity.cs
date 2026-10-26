namespace LT.Contracts.Entities;

/// <summary>
/// A named chart setup (timeframe, indicators, …), kept in settings and written as is to exported
/// files. Every setting is optional: one a template doesn't hold (saved before it existed) leaves
/// the chart's own as it is. Fields this version doesn't know are ignored on load.
/// </summary>
public class ChartTemplateEntity
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");
	public string Name { get; set; } = string.Empty;

	/// <summary>Format version it was written with (<c>ChartTemplateConstants.CurrentVersion</c>).</summary>
	public int Version { get; set; }

	public ChartTemplateSettingsEntity? Settings { get; set; }
}
