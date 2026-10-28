namespace LT.Contracts.Requests.Settings;

/// <summary>The content of a layout file picked for import, checked before anything changes.</summary>
public class ImportLayoutRequest
{
	public string Json { get; set; } = string.Empty;
}
