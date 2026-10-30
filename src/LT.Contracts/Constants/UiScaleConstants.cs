namespace LT.Contracts.Constants;

/// <summary>
/// App-wide UI scale (zoom), in percent of the normal size: the status bar picker and Settings →
/// Appearance. Applied to every window, popup and widget on top of the OS display scaling.
/// </summary>
public static class UiScaleConstants
{
	public const int DefaultPercent = 100;
	public const int MinPercent = 75;
	public const int MaxPercent = 200;

	/// <summary>The choices offered by both pickers, smallest first.</summary>
	public static readonly IReadOnlyList<int> StepPercents = [75, 90, 100, 110, 125, 150, 175, 200];
}
