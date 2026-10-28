namespace LT.Contracts.Entities;

/// <summary>
/// One drawing's part of an undo step (in memory only, never saved): the drawing before and after
/// the step — null before when it was added, null after when it was removed — and its place among
/// its contract's drawings (after for an added one, before for a removed one), so undoing a delete
/// puts it back where it was in the drawing order.
/// </summary>
public sealed record ChartDrawingChange(
	ChartDrawingEntity? Before,
	ChartDrawingEntity? After,
	int Index
);
