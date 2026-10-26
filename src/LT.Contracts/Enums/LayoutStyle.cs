namespace LT.Contracts.Enums;

/// <summary>How the workspace draws its widgets (Settings → Appearance).</summary>
public enum LayoutStyle
{
	/// <summary>Rounded tiles with gaps between them and around the center.</summary>
	Cards = 0,

	/// <summary>Square widgets filling the window edge to edge, split by thin lines.</summary>
	Flush = 1,
}
