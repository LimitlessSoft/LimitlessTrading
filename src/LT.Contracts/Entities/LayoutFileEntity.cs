namespace LT.Contracts.Entities;

/// <summary>
/// An exported layout: the layout section of the settings file as it is saved (every window,
/// view, docked and floating widget with its per-instance settings), with a format marker and
/// version. Nothing else of the settings goes in it (no connection, Flex token or account).
/// </summary>
public class LayoutFileEntity
{
	/// <summary><c>LayoutFileConstants.FileFormat</c>; anything else is not a layout file.</summary>
	public string? Format { get; set; }

	public int Version { get; set; }
	public LayoutEntity? Layout { get; set; }
}
