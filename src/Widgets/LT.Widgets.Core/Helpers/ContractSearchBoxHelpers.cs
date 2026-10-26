namespace LT.App.Helpers;

/// <summary>Key handling of <see cref="LT.App.Controls.ContractSearchBox"/>, UI-free.</summary>
public static class ContractSearchBoxHelpers
{
	/// <summary>
	/// Whether the box consumes an Esc. It does while there is something to cancel (typed text or
	/// an open dropdown), or always when <paramref name="passesIdleEscape"/> is off (a box shown
	/// only for an edit, e.g. a ticker title, where Esc ends the edit). An always-shown box (the
	/// watchlist's) lets an idle Esc through, so its widget's Esc still works (an unpinned docked
	/// widget collapses).
	/// </summary>
	public static bool ConsumesEscape(string? text, bool isDropDownOpen, bool passesIdleEscape) =>
		!passesIdleEscape || isDropDownOpen || !string.IsNullOrEmpty(text);
}
