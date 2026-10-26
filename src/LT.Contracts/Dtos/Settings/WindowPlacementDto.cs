using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Settings;

/// <summary>
/// Where a window is: its normal-state bounds (what un-maximizing goes back to), its state and the
/// screen it is on.
/// </summary>
public class WindowPlacementDto
{
	/// <summary>Normal-state position in device pixels; null = the window's default placement.</summary>
	public double? X { get; set; }
	public double? Y { get; set; }

	/// <summary>Normal-state size in DIPs.</summary>
	public double Width { get; set; }
	public double Height { get; set; }
	public WindowPlacementState State { get; set; }

	/// <summary>Null = not known (layouts saved before screens were).</summary>
	public WindowScreenDto? Screen { get; set; }
}
