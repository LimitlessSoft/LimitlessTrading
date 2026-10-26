namespace LT.Contracts.Dtos.Settings;

/// <summary>One tab of the center ("view"): a name and its own split tree (null = empty).</summary>
public class CenterViewDto
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public CenterNodeDto? Root { get; set; }
}
