using LT.Contracts.Entities;

namespace LT.Contracts.Helpers;

/// <summary>
/// Undo and redo of chart drawings: a step is the changes one edit made
/// (<see cref="ChartDrawingChange"/>), applied backwards to undo it and forwards to redo it. A
/// step's drawings are copies, so later edits of the live ones don't reach the history.
/// </summary>
public static class ChartDrawingHistoryHelpers
{
	/// <summary>A deep copy: points, style and levels are not shared.</summary>
	public static ChartDrawingEntity Clone(ChartDrawingEntity drawing) =>
		new()
		{
			Id = drawing.Id,
			ConId = drawing.ConId,
			Kind = drawing.Kind,
			Points =
			[
				.. drawing.Points.Select(x => new ChartDrawingPointEntity
				{
					Time = x.Time,
					Price = x.Price,
				}),
			],
			Text = drawing.Text,
			Style = drawing.Style is { } style
				? new ChartDrawingStyleEntity
				{
					Color = style.Color,
					LineStyle = style.LineStyle,
					LineWidth = style.LineWidth,
				}
				: null,
			IsLocked = drawing.IsLocked,
			IsHidden = drawing.IsHidden,
			FibLevels = drawing
				.FibLevels?.Select(x => new ChartDrawingFibLevelEntity
				{
					Value = x.Value,
					IsVisible = x.IsVisible,
					Color = x.Color,
				})
				.ToList(),
		};

	/// <summary>The drawing's place among its contract's drawings; -1 when it isn't there.</summary>
	public static int GetContractIndex(IReadOnlyList<ChartDrawingEntity> drawings, string id)
	{
		var drawing = drawings.FirstOrDefault(x => x.Id == id);
		if (drawing == null)
			return -1;
		var index = 0;
		foreach (var x in drawings)
		{
			if (x == drawing)
				return index;
			if (x.ConId == drawing.ConId)
				index++;
		}
		return -1;
	}

	/// <summary>
	/// Undoes (<paramref name="undo"/>) or redoes a step on the drawings of every contract: an
	/// added drawing is removed or put back, a removed one put back at its place or removed, a
	/// changed one set to its state before or after. A step's drawings are distinct, so removals
	/// and changes go first, then the put-backs in the order of their places (each place counts
	/// the ones before it). Copies go in, so the step can be applied again.
	/// </summary>
	public static void Apply(
		List<ChartDrawingEntity> drawings,
		IReadOnlyList<ChartDrawingChange> step,
		bool undo
	)
	{
		var inserts = new List<(ChartDrawingEntity Drawing, int Index)>();
		foreach (var change in step)
		{
			var from = undo ? change.After : change.Before;
			var to = undo ? change.Before : change.After;
			var id = (to ?? from)!.Id;
			var at = drawings.FindIndex(x => x.Id == id);
			if (to == null)
			{
				if (at >= 0)
					drawings.RemoveAt(at);
			}
			else if (at >= 0)
				drawings[at] = Clone(to);
			else
				inserts.Add((Clone(to), change.Index));
		}
		foreach (var (drawing, index) in inserts.OrderBy(x => x.Index))
			Insert(drawings, drawing, index);
	}

	/// <summary>
	/// A new step on the undo stack (the newest last; the oldest dropped above
	/// <paramref name="max"/>); the redo stack is cleared, as a new edit forks the history.
	/// </summary>
	public static void Push(
		List<IReadOnlyList<ChartDrawingChange>> undo,
		List<IReadOnlyList<ChartDrawingChange>> redo,
		IReadOnlyList<ChartDrawingChange> step,
		int max
	)
	{
		if (step.Count == 0)
			return;
		undo.Add(step);
		if (undo.Count > max)
			undo.RemoveRange(0, undo.Count - max);
		redo.Clear();
	}

	// At the index among its contract's drawings (after the last of them when past them)
	private static void Insert(
		List<ChartDrawingEntity> drawings,
		ChartDrawingEntity drawing,
		int index
	)
	{
		var seen = 0;
		for (var i = 0; i < drawings.Count; i++)
		{
			if (drawings[i].ConId != drawing.ConId)
				continue;
			if (seen == index)
			{
				drawings.Insert(i, drawing);
				return;
			}
			seen++;
		}
		drawings.Add(drawing);
	}
}
