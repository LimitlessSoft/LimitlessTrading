using LT.Contracts.Constants;

namespace LT.Contracts.Helpers;

public static class UiScaleHelpers
{
	/// <summary>A saved percent brought into the allowed range (a hand-edited settings file).</summary>
	public static int Clamp(int percent) =>
		Math.Clamp(percent, UiScaleConstants.MinPercent, UiScaleConstants.MaxPercent);

	/// <summary>
	/// The pickers' choices: the steps, plus <paramref name="current"/> in order when it is not one
	/// of them (a value typed into settings.json), so the picker can still show it.
	/// </summary>
	public static IReadOnlyList<int> Options(int current) =>
		UiScaleConstants.StepPercents.Contains(current)
			? UiScaleConstants.StepPercents
			: [.. UiScaleConstants.StepPercents.Append(current).Order()];
}
