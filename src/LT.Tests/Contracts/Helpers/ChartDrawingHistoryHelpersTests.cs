using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>
/// The undo stack's steps: applied backwards and forwards, a removed drawing goes back to its
/// place among its contract's, copies keep the history apart from the live drawings, and a new
/// step drops the redo stack and the oldest steps above the limit.
/// </summary>
public class ChartDrawingHistoryHelpersTests
{
	private static readonly DateTimeOffset Time = new(2026, 10, 27, 14, 0, 0, TimeSpan.Zero);

	[Fact]
	public void Undoing_a_delete_puts_the_drawing_back_at_its_place_among_its_contract_s()
	{
		List<ChartDrawingEntity> drawings = [D("a", 1), D("x", 2), D("b", 1), D("c", 1)];
		var removed = drawings[2];
		var step = new[]
		{
			new ChartDrawingChange(
				ChartDrawingHistoryHelpers.Clone(removed),
				null,
				ChartDrawingHistoryHelpers.GetContractIndex(drawings, "b")
			),
		};
		drawings.Remove(removed);

		ChartDrawingHistoryHelpers.Apply(drawings, step, undo: true);
		Assert.Equal(["a", "b", "c"], drawings.Where(x => x.ConId == 1).Select(x => x.Id));

		ChartDrawingHistoryHelpers.Apply(drawings, step, undo: false);
		Assert.Equal(["a", "c"], drawings.Where(x => x.ConId == 1).Select(x => x.Id));
		Assert.Equal("x", Assert.Single(drawings, x => x.ConId == 2).Id);
	}

	[Fact]
	public void Undoing_a_change_restores_the_drawing_and_redoing_it_changes_it_again()
	{
		var live = D("a", 1, price: 100);
		List<ChartDrawingEntity> drawings = [live];
		var before = ChartDrawingHistoryHelpers.Clone(live);
		live.Points[0].Price = 150;
		live.Style = new() { Color = "#F23645", LineWidth = 2 };
		var step = new[]
		{
			new ChartDrawingChange(before, ChartDrawingHistoryHelpers.Clone(live), 0),
		};

		ChartDrawingHistoryHelpers.Apply(drawings, step, undo: true);
		Assert.Equal(100, drawings[0].Points[0].Price);
		Assert.Null(drawings[0].Style);

		// The step's copies don't follow later edits of the live drawing
		drawings[0].Points[0].Price = 999;
		ChartDrawingHistoryHelpers.Apply(drawings, step, undo: false);
		Assert.Equal(150, drawings[0].Points[0].Price);
		Assert.Equal("#F23645", drawings[0].Style!.Color);
	}

	[Fact]
	public void Undoing_remove_all_puts_every_drawing_back_in_order()
	{
		List<ChartDrawingEntity> drawings = [D("a", 1), D("x", 2), D("b", 1), D("c", 1)];
		var step = drawings
			.Where(x => x.ConId == 1)
			.Select((x, i) => new ChartDrawingChange(ChartDrawingHistoryHelpers.Clone(x), null, i))
			.ToList();
		drawings.RemoveAll(x => x.ConId == 1);

		ChartDrawingHistoryHelpers.Apply(drawings, step, undo: true);

		Assert.Equal(["a", "b", "c"], drawings.Where(x => x.ConId == 1).Select(x => x.Id));
	}

	[Fact]
	public void A_new_step_clears_redo_and_the_oldest_go_above_the_limit()
	{
		List<IReadOnlyList<ChartDrawingChange>> undo = [];
		List<IReadOnlyList<ChartDrawingChange>> redo =
		[
			[new ChartDrawingChange(null, D("r", 1), 0)],
		];

		for (var i = 0; i < 5; i++)
			ChartDrawingHistoryHelpers.Push(
				undo,
				redo,
				[new ChartDrawingChange(null, D($"s{i}", 1), i)],
				3
			);
		ChartDrawingHistoryHelpers.Push(undo, redo, [], 3);

		Assert.Empty(redo);
		Assert.Equal(["s2", "s3", "s4"], undo.Select(x => x[0].After!.Id));
	}

	[Fact]
	public void A_clone_shares_nothing()
	{
		var drawing = D("a", 1);
		drawing.Style = new() { Color = "#2962FF" };
		drawing.FibLevels = [new() { Value = 0.5 }];

		var clone = ChartDrawingHistoryHelpers.Clone(drawing);
		clone.Points[0].Price = 1;
		clone.Style!.Color = "#000000";
		clone.FibLevels![0].Value = 2;

		Assert.Equal(100, drawing.Points[0].Price);
		Assert.Equal("#2962FF", drawing.Style.Color);
		Assert.Equal(0.5, drawing.FibLevels[0].Value);
	}

	private static ChartDrawingEntity D(string id, int conId, double price = 100) =>
		new()
		{
			Id = id,
			ConId = conId,
			Kind = ChartDrawingKind.HorizontalLine,
			Points = [new() { Time = Time, Price = price }],
		};
}
