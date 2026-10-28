using LSCore.Exceptions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.ChartDrawings;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

/// <summary>
/// Drawing styles, lock / hide and Fibonacci levels: a new drawing starts with its kind's last
/// used style, the fields are validated, old drawings read with the built-in style and levels.
/// </summary>
public class ChartDrawingManagerStyleTests
{
	private const int Aapl = 265598;
	private static readonly DateTimeOffset Time = new(2026, 10, 27, 14, 0, 0, TimeSpan.Zero);

	private static readonly ChartDrawingStyleDto Red = new()
	{
		Color = "#f23645",
		LineStyle = ChartDrawingLineStyle.Dotted,
		LineWidth = 2,
	};

	private readonly InMemoryChartDrawingRepository _repository = new();
	private readonly ChartDrawingManager _manager;

	public ChartDrawingManagerStyleTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new ChartDrawingManager(_repository);
	}

	[Fact]
	public void A_new_drawing_starts_with_the_style_last_used_for_its_kind()
	{
		var first = Add(ChartDrawingKind.TrendLine);
		Assert.Equal(ChartDrawingConstants.DefaultColor, first.Style.Color);

		_manager.UpdateProperties(
			new UpdateChartDrawingPropertiesRequest { Id = first.Id, Style = Red }
		);
		var second = Add(ChartDrawingKind.TrendLine);
		var other = Add(ChartDrawingKind.Rectangle);

		Assert.Equal("#F23645", second.Style.Color);
		Assert.Equal(ChartDrawingLineStyle.Dotted, second.Style.LineStyle);
		Assert.Equal(2, second.Style.LineWidth);
		Assert.Equal(ChartDrawingConstants.DefaultColor, other.Style.Color);
		// Kept across restarts
		Assert.Equal(
			"#F23645",
			new ChartDrawingManager(_repository).GetDefaultStyle(ChartDrawingKind.TrendLine).Color
		);
	}

	[Fact]
	public void A_drawing_keeps_its_style_when_the_kind_s_default_changes()
	{
		var first = Add(ChartDrawingKind.TrendLine);
		var second = Add(ChartDrawingKind.TrendLine);

		_manager.UpdateProperties(
			new UpdateChartDrawingPropertiesRequest { Id = second.Id, Style = Red }
		);

		Assert.Equal(
			ChartDrawingConstants.DefaultColor,
			_manager.GetDrawings(Aapl).Single(x => x.Id == first.Id).Style.Color
		);
	}

	[Theory]
	[InlineData("red", 2, ChartDrawingLineStyle.Solid)]
	[InlineData("#12345", 2, ChartDrawingLineStyle.Solid)]
	[InlineData("#2962FF", 0.5, ChartDrawingLineStyle.Solid)]
	[InlineData("#2962FF", 5, ChartDrawingLineStyle.Solid)]
	[InlineData("#2962FF", 2, (ChartDrawingLineStyle)9)]
	public void An_invalid_style_is_refused(string color, double width, ChartDrawingLineStyle style)
	{
		var line = Add(ChartDrawingKind.TrendLine);
		var bad = new ChartDrawingStyleDto
		{
			Color = color,
			LineWidth = width,
			LineStyle = style,
		};

		Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.UpdateProperties(
				new UpdateChartDrawingPropertiesRequest { Id = line.Id, Style = bad }
			)
		);
		Assert.Throws<LSCoreBadRequestException>(() => Add(ChartDrawingKind.TrendLine, style: bad));
		Assert.Equal(ChartDrawingConstants.DefaultColor, _manager.GetDrawings(Aapl)[0].Style.Color);
	}

	[Fact]
	public void Lock_and_hide_are_kept_per_drawing()
	{
		var line = Add(ChartDrawingKind.TrendLine);
		var other = Add(ChartDrawingKind.Rectangle);

		_manager.UpdateProperties(
			new UpdateChartDrawingPropertiesRequest { Id = line.Id, IsLocked = true }
		);
		_manager.UpdateProperties(
			new UpdateChartDrawingPropertiesRequest { Id = other.Id, IsHidden = true }
		);

		var drawings = _manager.GetDrawings(Aapl);
		Assert.True(drawings[0].IsLocked);
		Assert.False(drawings[0].IsHidden);
		Assert.True(drawings[1].IsHidden);
		Assert.Equal(1, _manager.ShowHidden(Aapl));
		Assert.False(_manager.GetDrawings(Aapl)[1].IsHidden);
		Assert.Equal(0, _manager.ShowHidden(Aapl));
	}

	[Fact]
	public void A_fib_drawing_has_the_default_levels_until_edited()
	{
		var fib = Add(ChartDrawingKind.FibRetracement);

		Assert.Equal(
			ChartDrawingConstants.FibRetracementLevels,
			fib.FibLevels.Select(x => x.Value)
		);
		Assert.Null(_repository.Drawings[0].FibLevels);
		Assert.Empty(Add(ChartDrawingKind.TrendLine).FibLevels);
	}

	[Fact]
	public void Edited_levels_are_kept_with_their_colours_and_switches()
	{
		var fib = Add(ChartDrawingKind.FibExtension);

		_manager.UpdateProperties(
			new UpdateChartDrawingPropertiesRequest
			{
				Id = fib.Id,
				FibLevels =
				[
					new() { Value = 0 },
					new()
					{
						Value = 1.414,
						Color = "#ff9800",
						IsVisible = false,
					},
				],
			}
		);

		var levels = new ChartDrawingManager(_repository).GetDrawings(Aapl)[0].FibLevels;
		Assert.Equal([0, 1.414], levels.Select(x => x.Value));
		Assert.Null(levels[0].Color);
		Assert.Equal("#FF9800", levels[1].Color);
		Assert.False(levels[1].IsVisible);
	}

	[Fact]
	public void Levels_are_ignored_on_drawings_that_are_not_fib()
	{
		var line = Add(ChartDrawingKind.TrendLine);

		_manager.UpdateProperties(
			new UpdateChartDrawingPropertiesRequest
			{
				Id = line.Id,
				FibLevels = [new() { Value = 0.5 }],
			}
		);

		Assert.Empty(_manager.GetDrawings(Aapl)[0].FibLevels);
		Assert.Null(_repository.Drawings[0].FibLevels);
	}

	// None, too many, out of range, not a number, twice, a colour that isn't one
	private static List<ChartDrawingFibLevelDto> BadLevels(int which) =>
		which switch
		{
			0 => [],
			1 =>
			[
				.. Enumerable
					.Range(0, ChartDrawingConstants.MaxFibLevels + 1)
					.Select(i => new ChartDrawingFibLevelDto { Value = i * 0.1 }),
			],
			2 => [new() { Value = ChartDrawingConstants.MaxFibLevel + 1 }],
			3 => [new() { Value = double.NaN }],
			4 => [new() { Value = 0.5 }, new() { Value = 0.5 }],
			_ => [new() { Value = 0.5, Color = "orange" }],
		};

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	[InlineData(5)]
	public void Invalid_levels_are_refused(int which)
	{
		var levels = BadLevels(which);
		var fib = Add(ChartDrawingKind.FibRetracement);

		Assert.Throws<LSCoreBadRequestException>(() =>
			_manager.UpdateProperties(
				new UpdateChartDrawingPropertiesRequest { Id = fib.Id, FibLevels = levels }
			)
		);
		Assert.Null(_repository.Drawings[0].FibLevels);
	}

	[Fact]
	public void A_drawing_saved_before_styles_reads_with_the_built_in_ones()
	{
		_repository.Drawings =
		[
			new ChartDrawingEntity
			{
				Id = "old",
				ConId = Aapl,
				Kind = ChartDrawingKind.FibRetracement,
				Points =
				[
					new() { Time = Time, Price = 100 },
					new() { Time = Time.AddMinutes(5), Price = 110 },
				],
			},
		];

		var drawing = Assert.Single(new ChartDrawingManager(_repository).GetDrawings(Aapl));

		Assert.Equal(ChartDrawingConstants.DefaultColor, drawing.Style.Color);
		Assert.Equal(ChartDrawingLineStyle.Solid, drawing.Style.LineStyle);
		Assert.False(drawing.IsLocked);
		Assert.False(drawing.IsHidden);
		Assert.Equal(
			ChartDrawingConstants.FibRetracementLevels,
			drawing.FibLevels.Select(x => x.Value)
		);
	}

	private ChartDrawingDto Add(ChartDrawingKind kind, ChartDrawingStyleDto? style = null) =>
		_manager.Create(
			new CreateChartDrawingRequest
			{
				ConId = Aapl,
				Kind = kind,
				Points =
				[
					.. Enumerable
						.Range(0, ChartDrawingHelpers.GetPointCount(kind))
						.Select(i => new ChartDrawingPointDto
						{
							Time = Time.AddMinutes(5 * i),
							Price = 100 + i,
						}),
				],
				Style = style,
			}
		);
}
