namespace LT.Contracts.Entities;

/// <summary>One tab of the center ("view"): a name and its own split tree.</summary>
public class CenterViewEntity
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");
	public string Name { get; set; } = string.Empty;

	/// <summary>The view's split tree; null = the view is empty.</summary>
	public CenterNodeEntity? Root { get; set; }
}
