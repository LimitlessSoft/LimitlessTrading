using LT.Contracts.Constants;
using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Enums;
using LT.Contracts.Requests.ChartDrawings;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

/// <summary>
/// Undo / redo of drawings, per contract: create, move, reshape, text edit, style change, delete
/// and remove all each step back and forth; a new change drops what was undone; every step is
/// saved and announced.
/// </summary>
public class ChartDrawingManagerHistoryTests
{
	private const int Aapl = 265598;
	private const int Msft = 272093;
	private static readonly DateTimeOffset Time = new(2026, 10, 27, 14, 0, 0, TimeSpan.Zero);

	private readonly InMemoryChartDrawingRepository _repository = new();
	private readonly ChartDrawingManager _manager;

	public ChartDrawingManagerHistoryTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new ChartDrawingManager(_repository);
	}

	[Fact]
	public void Nothing_to_undo_or_redo_at_first()
	{
		Assert.False(_manager.CanUndo(Aapl));
		Assert.False(_manager.CanRedo(Aapl));
		Assert.False(_manager.Undo(Aapl));
		Assert.False(_manager.Redo(Aapl));
	}

	[Fact]
	public void Undoing_a_create_removes_it_and_redoing_brings_back_the_same_drawing()
	{
		var line = Add(Aapl, ChartDrawingKind.TrendLine, 2);

		Assert.True(_manager.Undo(Aapl));
		Assert.Empty(_manager.GetDrawings(Aapl));
		Assert.Empty(_repository.Drawings);
		Assert.True(_manager.CanRedo(Aapl));

		Assert.True(_manager.Redo(Aapl));
		Assert.Equal(line.Id, Assert.Single(_manager.GetDrawings(Aapl)).Id);
		Assert.False(_manager.CanRedo(Aapl));
	}

	[Fact]
	public void Moves_reshapes_and_text_edits_step_back_one_at_a_time()
	{
		var note = Add(Aapl, ChartDrawingKind.Text, 1, text: "Gap");
		_manager.Update(new UpdateChartDrawingRequest { Id = note.Id, Points = Points(1, 150) });
		_manager.Update(
			new UpdateChartDrawingRequest
			{
				Id = note.Id,
				Points = Points(1, 150),
				Text = "Gap filled",
			}
		);

		_manager.Undo(Aapl);
		var drawing = Assert.Single(_manager.GetDrawings(Aapl));
		Assert.Equal("Gap", drawing.Text);
		Assert.Equal(150, drawing.Points[0].Price);

		_manager.Undo(Aapl);
		Assert.Equal(100, _manager.GetDrawings(Aapl)[0].Points[0].Price);

		_manager.Redo(Aapl);
		_manager.Redo(Aapl);
		drawing = _manager.GetDrawings(Aapl)[0];
		Assert.Equal("Gap filled", drawing.Text);
		Assert.Equal(150, drawing.Points[0].Price);
	}

	[Fact]
	public void Undoing_a_style_change_lock_or_hide_restores_them()
	{
		var line = Add(Aapl, ChartDrawingKind.TrendLine, 2);
		_manager.UpdateProperties(
			new UpdateChartDrawingPropertiesRequest
			{
				Id = line.Id,
				Style = new ChartDrawingStyleDto
				{
					Color = "#F23645",
					LineStyle = ChartDrawingLineStyle.Dashed,
					LineWidth = 3,
				},
			}
		);
		_manager.UpdateProperties(
			new UpdateChartDrawingPropertiesRequest
			{
				Id = line.Id,
				IsLocked = true,
				IsHidden = true,
			}
		);

		_manager.Undo(Aapl);
		var drawing = _manager.GetDrawings(Aapl)[0];
		Assert.False(drawing.IsLocked);
		Assert.False(drawing.IsHidden);
		Assert.Equal("#F23645", drawing.Style.Color);

		_manager.Undo(Aapl);
		drawing = _manager.GetDrawings(Aapl)[0];
		Assert.Equal(ChartDrawingConstants.DefaultColor, drawing.Style.Color);
		Assert.Equal(ChartDrawingLineStyle.Solid, drawing.Style.LineStyle);
	}

	[Fact]
	public void Undoing_a_delete_puts_it_back_in_its_place_in_the_drawing_order()
	{
		var first = Add(Aapl, ChartDrawingKind.TrendLine, 2);
		var middle = Add(Aapl, ChartDrawingKind.Rectangle, 2);
		var last = Add(Aapl, ChartDrawingKind.Ellipse, 2);

		_manager.Delete(middle.Id);
		_manager.Undo(Aapl);

		Assert.Equal([first.Id, middle.Id, last.Id], _manager.GetDrawings(Aapl).Select(x => x.Id));
	}

	[Fact]
	public void Undoing_remove_all_brings_every_drawing_back_as_one_step()
	{
		var first = Add(Aapl, ChartDrawingKind.TrendLine, 2);
		var second = Add(Aapl, ChartDrawingKind.Rectangle, 2);
		Add(Msft, ChartDrawingKind.HorizontalLine, 1);

		_manager.DeleteAll(Aapl);
		_manager.Undo(Aapl);

		Assert.Equal([first.Id, second.Id], _manager.GetDrawings(Aapl).Select(x => x.Id));
		Assert.Single(_manager.GetDrawings(Msft));
		_manager.Redo(Aapl);
		Assert.Empty(_manager.GetDrawings(Aapl));
	}

	[Fact]
	public void The_history_is_per_contract()
	{
		Add(Aapl, ChartDrawingKind.TrendLine, 2);
		Add(Msft, ChartDrawingKind.HorizontalLine, 1);

		_manager.Undo(Msft);

		Assert.Single(_manager.GetDrawings(Aapl));
		Assert.Empty(_manager.GetDrawings(Msft));
		Assert.True(_manager.CanUndo(Aapl));
		Assert.False(_manager.CanUndo(Msft));
	}

	[Fact]
	public void A_new_change_after_an_undo_drops_the_redo()
	{
		Add(Aapl, ChartDrawingKind.TrendLine, 2);
		_manager.Undo(Aapl);

		Add(Aapl, ChartDrawingKind.Rectangle, 2);

		Assert.False(_manager.CanRedo(Aapl));
		Assert.Equal(ChartDrawingKind.Rectangle, Assert.Single(_manager.GetDrawings(Aapl)).Kind);
	}

	[Fact]
	public void At_most_the_limit_of_steps_is_kept()
	{
		for (var i = 0; i < ChartDrawingConstants.MaxUndoSteps + 5; i++)
			Add(Aapl, ChartDrawingKind.HorizontalLine, 1);

		var undone = 0;
		while (_manager.Undo(Aapl))
			undone++;

		Assert.Equal(ChartDrawingConstants.MaxUndoSteps, undone);
		Assert.Equal(5, _manager.GetDrawings(Aapl).Count);
	}

	[Fact]
	public void A_refused_change_is_no_step()
	{
		var line = Add(Aapl, ChartDrawingKind.TrendLine, 2);
		Assert.ThrowsAny<Exception>(() =>
			_manager.Update(new UpdateChartDrawingRequest { Id = line.Id, Points = Points(3) })
		);

		_manager.Undo(Aapl);

		Assert.Empty(_manager.GetDrawings(Aapl));
		Assert.False(_manager.CanUndo(Aapl));
	}

	[Fact]
	public void Undo_and_redo_are_saved_and_announced()
	{
		Add(Aapl, ChartDrawingKind.TrendLine, 2);
		var saves = _repository.SaveCount;
		var changes = 0;
		_manager.DrawingsChanged += () => changes++;

		_manager.Undo(Aapl);
		_manager.Redo(Aapl);
		_manager.Redo(Aapl);

		Assert.Equal(2, changes);
		Assert.Equal(saves + 2, _repository.SaveCount);
		Assert.Single(_repository.Drawings);
	}

	private ChartDrawingDto Add(int conId, ChartDrawingKind kind, int count, string? text = null) =>
		_manager.Create(
			new CreateChartDrawingRequest
			{
				ConId = conId,
				Kind = kind,
				Points = Points(count),
				Text = text,
			}
		);

	private static List<ChartDrawingPointDto> Points(int count, double price = 100) =>
		[
			.. Enumerable
				.Range(0, count)
				.Select(i => new ChartDrawingPointDto
				{
					Time = Time.AddMinutes(5 * i),
					Price = price + i,
				}),
		];
}
