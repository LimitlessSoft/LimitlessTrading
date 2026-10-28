using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Repository.Repositories;

namespace LT.Tests.Repository;

/// <summary>The real chart-drawings.json, in a temp folder deleted after each test.</summary>
public sealed class ChartDrawingRepositoryTests : IDisposable
{
	private readonly string _directory = Path.Combine(
		Path.GetTempPath(),
		$"lt-chart-drawings-tests-{Guid.NewGuid():N}"
	);

	private string FilePath => Path.Combine(_directory, "chart-drawings.json");

	public void Dispose()
	{
		if (Directory.Exists(_directory))
			Directory.Delete(_directory, recursive: true);
	}

	[Fact]
	public void Missing_file_has_no_drawings() =>
		Assert.Empty(new ChartDrawingRepository(FilePath).Get().Drawings);

	[Fact]
	public void Unreadable_file_has_no_drawings()
	{
		Directory.CreateDirectory(_directory);
		File.WriteAllText(FilePath, "{ not json");

		Assert.Empty(new ChartDrawingRepository(FilePath).Get().Drawings);
	}

	[Fact]
	public void Round_trips_the_drawings()
	{
		var time = new DateTimeOffset(2026, 10, 27, 14, 0, 0, TimeSpan.Zero);
		new ChartDrawingRepository(FilePath).Save(
			new ChartDrawingFileEntity
			{
				Drawings =
				[
					new ChartDrawingEntity
					{
						Id = "a",
						ConId = 265598,
						Kind = ChartDrawingKind.FibExtension,
						Points =
						[
							new() { Time = time, Price = 100 },
							new() { Time = time.AddMinutes(5), Price = 150 },
							new() { Time = time.AddMinutes(10), Price = 120.5 },
						],
					},
					new ChartDrawingEntity
					{
						Id = "b",
						ConId = 265598,
						Kind = ChartDrawingKind.Text,
						Points = [new() { Time = time, Price = 101 }],
						Text = "Gap",
					},
				],
			}
		);

		var drawings = new ChartDrawingRepository(FilePath).Get().Drawings;

		Assert.Equal(["a", "b"], drawings.Select(x => x.Id));
		Assert.Equal(ChartDrawingKind.FibExtension, drawings[0].Kind);
		Assert.Equal(120.5, drawings[0].Points[2].Price);
		Assert.Equal(time.AddMinutes(10), drawings[0].Points[2].Time);
		Assert.Equal("Gap", drawings[1].Text);
		Assert.False(File.Exists(FilePath + ".tmp"));
	}

	[Fact]
	public void A_file_written_before_styles_loads_with_the_defaults()
	{
		Directory.CreateDirectory(_directory);
		// chart-drawings.json as the first drawing version wrote it
		File.WriteAllText(
			FilePath,
			"""
			{
			  "Drawings": [
			    {
			      "Id": "old",
			      "ConId": 265598,
			      "Kind": 11,
			      "Points": [
			        { "Time": "2026-10-27T14:00:00+00:00", "Price": 100 },
			        { "Time": "2026-10-27T14:05:00+00:00", "Price": 110 }
			      ],
			      "Text": null
			    }
			  ]
			}
			"""
		);

		var file = new ChartDrawingRepository(FilePath).Get();

		var drawing = Assert.Single(file.Drawings);
		Assert.Equal(ChartDrawingKind.FibRetracement, drawing.Kind);
		Assert.Null(drawing.Style);
		Assert.Null(drawing.FibLevels);
		Assert.False(drawing.IsLocked);
		Assert.False(drawing.IsHidden);
		Assert.Empty(file.ToolStyles);
	}

	[Fact]
	public void Round_trips_styles_levels_lock_hide_and_tool_styles()
	{
		var time = new DateTimeOffset(2026, 10, 27, 14, 0, 0, TimeSpan.Zero);
		new ChartDrawingRepository(FilePath).Save(
			new ChartDrawingFileEntity
			{
				Drawings =
				[
					new ChartDrawingEntity
					{
						Id = "a",
						ConId = 265598,
						Kind = ChartDrawingKind.FibRetracement,
						Points =
						[
							new() { Time = time, Price = 100 },
							new() { Time = time.AddMinutes(5), Price = 110 },
						],
						Style = new()
						{
							Color = "#F23645",
							LineStyle = ChartDrawingLineStyle.Dashed,
							LineWidth = 2,
						},
						IsLocked = true,
						IsHidden = true,
						FibLevels =
						[
							new()
							{
								Value = 0.65,
								IsVisible = false,
								Color = "#FF9800",
							},
						],
					},
				],
				ToolStyles =
				[
					new()
					{
						Kind = ChartDrawingKind.Ray,
						Style = new() { Color = "#089981", LineWidth = 3 },
					},
				],
			}
		);

		var file = new ChartDrawingRepository(FilePath).Get();

		var drawing = Assert.Single(file.Drawings);
		Assert.Equal(ChartDrawingLineStyle.Dashed, drawing.Style!.LineStyle);
		Assert.Equal("#F23645", drawing.Style.Color);
		Assert.True(drawing.IsLocked);
		Assert.True(drawing.IsHidden);
		var level = Assert.Single(drawing.FibLevels!);
		Assert.Equal(0.65, level.Value);
		Assert.False(level.IsVisible);
		Assert.Equal("#FF9800", level.Color);
		var toolStyle = Assert.Single(file.ToolStyles);
		Assert.Equal(ChartDrawingKind.Ray, toolStyle.Kind);
		Assert.Equal(3, toolStyle.Style.LineWidth);
	}
}
