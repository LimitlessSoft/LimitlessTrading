using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Requests.ChartDrawings;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

/// <summary>
/// Chart drawings: kept per contract, refused when their points don't fit the kind or a note has
/// no text, moved and removed, and saved across restarts.
/// </summary>
public class ChartDrawingManagerTests
{
	private const int Aapl = 265598;
	private const int Msft = 272093;
	private static readonly DateTimeOffset Time = new(2026, 10, 27, 14, 0, 0, TimeSpan.Zero);

	private readonly InMemoryChartDrawingRepository _repository = new();
	private readonly ChartDrawingManager _manager;

	public ChartDrawingManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new ChartDrawingManager(_repository);
	}

	[Fact]
	public void Drawings_are_kept_per_contract_and_saved()
	{
		var line = Add(Aapl, ChartDrawingKind.TrendLine, 2);
		Add(Msft, ChartDrawingKind.HorizontalLine, 1);

		var drawing = Assert.Single(_manager.GetDrawings(Aapl));
		Assert.Equal(line.Id, drawing.Id);
		Assert.Equal(ChartDrawingKind.TrendLine, drawing.Kind);
		Assert.Equal([100d, 101d], drawing.Points.Select(x => x.Price));
		Assert.Equal(2, _repository.Drawings.Count);
	}

	[Fact]
	public void Drawings_are_read_back_after_a_restart()
	{
		var fib = Add(Aapl, ChartDrawingKind.FibRetracement, 2);

		var restarted = new ChartDrawingManager(_repository);

		var drawing = Assert.Single(restarted.GetDrawings(Aapl));
		Assert.Equal(fib.Id, drawing.Id);
		Assert.Equal(Time, drawing.Points[0].Time);
	}

	[Theory]
	[InlineData(ChartDrawingKind.TrendLine, 1)]
	[InlineData(ChartDrawingKind.TrendLine, 3)]
	[InlineData(ChartDrawingKind.HorizontalLine, 2)]
	[InlineData(ChartDrawingKind.ParallelChannel, 2)]
	[InlineData(ChartDrawingKind.Brush, 1)]
	public void Points_that_do_not_fit_the_kind_are_refused(ChartDrawingKind kind, int count) =>
		Assert.Throws<LSCoreBadRequestException>(() => Add(Aapl, kind, count));

	[Fact]
	public void A_drawing_needs_a_contract_a_known_kind_and_finite_prices()
	{
		Assert.Throws<LSCoreBadRequestException>(() => Add(0, ChartDrawingKind.TrendLine, 2));
		Assert.Throws<LSCoreBadRequestException>(() => Add(Aapl, (ChartDrawingKind)999, 2));
		Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.Create(
				new CreateChartDrawingRequest
				{
					ConId = Aapl,
					Kind = ChartDrawingKind.HorizontalLine,
					Points = [new ChartDrawingPointDto { Time = Time, Price = double.NaN }],
				}
			)
		);
		Assert.Empty(_repository.Drawings);
	}

	[Fact]
	public void A_note_needs_its_text_which_is_trimmed()
	{
		Assert.Throws<LSCoreBadRequestException>(() =>
			Add(Aapl, ChartDrawingKind.Text, 1, text: "  ")
		);
		Assert.Throws<LSCoreBadRequestException>(() =>
			Add(
				Aapl,
				ChartDrawingKind.Text,
				1,
				text: new string('x', ChartDrawingConstants.MaxTextLength + 1)
			)
		);

		var note = Add(Aapl, ChartDrawingKind.Text, 1, text: "  Earnings gap ");

		Assert.Equal("Earnings gap", note.Text);
	}

	[Fact]
	public void Only_a_note_keeps_a_text()
	{
		var line = Add(Aapl, ChartDrawingKind.TrendLine, 2, text: "ignored");

		Assert.Null(line.Text);
	}

	[Fact]
	public void A_contract_holds_at_most_the_limit_of_drawings()
	{
		_repository.Drawings =
		[
			.. Enumerable
				.Range(0, ChartDrawingConstants.MaxDrawingsPerContract)
				.Select(i => new ChartDrawingEntity
				{
					Id = $"d{i}",
					ConId = Aapl,
					Kind = ChartDrawingKind.HorizontalLine,
					Points = [new ChartDrawingPointEntity { Time = Time, Price = 100 }],
				}),
		];

		Assert.Throws<LSCoreBadRequestException>(() =>
			Add(Aapl, ChartDrawingKind.HorizontalLine, 1)
		);
		Add(Msft, ChartDrawingKind.HorizontalLine, 1);
	}

	[Fact]
	public void Moving_a_drawing_replaces_its_points_and_a_note_keeps_its_text()
	{
		var line = Add(Aapl, ChartDrawingKind.TrendLine, 2);
		var note = Add(Aapl, ChartDrawingKind.Text, 1, text: "Breakout");

		_manager.Update(new UpdateChartDrawingRequest { Id = line.Id, Points = Points(2, 150) });
		_manager.Update(new UpdateChartDrawingRequest { Id = note.Id, Points = Points(1, 160) });
		_manager.Update(
			new UpdateChartDrawingRequest
			{
				Id = note.Id,
				Points = Points(1, 160),
				Text = "Retest",
			}
		);

		var drawings = _manager.GetDrawings(Aapl);
		Assert.Equal([150d, 151d], drawings[0].Points.Select(x => x.Price));
		Assert.Equal(160, drawings[1].Points[0].Price);
		Assert.Equal("Retest", drawings[1].Text);
	}

	[Fact]
	public void A_move_must_keep_the_kind_s_points_and_the_drawing_must_exist()
	{
		var line = Add(Aapl, ChartDrawingKind.TrendLine, 2);

		Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.Update(new UpdateChartDrawingRequest { Id = line.Id, Points = Points(3) })
		);
		Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.Update(new UpdateChartDrawingRequest { Id = "gone", Points = Points(2) })
		);
		Assert.Equal([100d, 101d], _manager.GetDrawings(Aapl)[0].Points.Select(x => x.Price));
	}

	[Fact]
	public void Deleting_removes_one_and_delete_all_only_the_contract_s()
	{
		var first = Add(Aapl, ChartDrawingKind.TrendLine, 2);
		Add(Aapl, ChartDrawingKind.Rectangle, 2);
		Add(Aapl, ChartDrawingKind.Ellipse, 2);
		Add(Msft, ChartDrawingKind.Triangle, 3);

		_manager.Delete(first.Id);
		_manager.Delete("gone");
		Assert.Equal(2, _manager.GetDrawings(Aapl).Count);

		Assert.Equal(2, _manager.DeleteAll(Aapl));
		Assert.Equal(0, _manager.DeleteAll(Aapl));
		Assert.Empty(_manager.GetDrawings(Aapl));
		Assert.Single(_manager.GetDrawings(Msft));
		Assert.Single(_repository.Drawings);
	}

	[Fact]
	public void Every_change_is_announced_and_nothing_else_is()
	{
		var changes = 0;
		_manager.DrawingsChanged += () => changes++;

		var line = Add(Aapl, ChartDrawingKind.TrendLine, 2);
		_manager.Update(new UpdateChartDrawingRequest { Id = line.Id, Points = Points(2, 120) });
		_manager.Delete("gone");
		_manager.DeleteAll(Msft);
		_manager.Delete(line.Id);

		Assert.Equal(3, changes);
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
