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
		Assert.Empty(new ChartDrawingRepository(FilePath).Get());

	[Fact]
	public void Unreadable_file_has_no_drawings()
	{
		Directory.CreateDirectory(_directory);
		File.WriteAllText(FilePath, "{ not json");

		Assert.Empty(new ChartDrawingRepository(FilePath).Get());
	}

	[Fact]
	public void Round_trips_the_drawings()
	{
		var time = new DateTimeOffset(2026, 10, 27, 14, 0, 0, TimeSpan.Zero);
		new ChartDrawingRepository(FilePath).Save([
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
		]);

		var drawings = new ChartDrawingRepository(FilePath).Get();

		Assert.Equal(["a", "b"], drawings.Select(x => x.Id));
		Assert.Equal(ChartDrawingKind.FibExtension, drawings[0].Kind);
		Assert.Equal(120.5, drawings[0].Points[2].Price);
		Assert.Equal(time.AddMinutes(10), drawings[0].Points[2].Time);
		Assert.Equal("Gap", drawings[1].Text);
		Assert.False(File.Exists(FilePath + ".tmp"));
	}
}
