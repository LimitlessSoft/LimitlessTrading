namespace LT.Contracts.Enums;

/// <summary>
/// How a window comes back: a minimized window is saved as the state it was in before (normal
/// when unknown), so it never reopens minimized.
/// </summary>
public enum WindowPlacementState
{
	Normal = 0,
	Maximized = 1,
	FullScreen = 2,
}
