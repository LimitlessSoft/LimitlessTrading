using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Enums;
using LT.Repository.Repositories;

namespace LT.Tests.Repository;

/// <summary>The real footprint trade cache, in a temp folder deleted after each test.</summary>
public sealed class FootprintTapeRepositoryTests : IDisposable
{
	private const int ConId = 756733;
	private static readonly DateTimeOffset T0 = new(2026, 10, 28, 13, 30, 0, TimeSpan.Zero);

	private readonly string _directory = Path.Combine(
		Path.GetTempPath(),
		$"lt-tape-tests-{Guid.NewGuid():N}"
	);

	public void Dispose()
	{
		if (Directory.Exists(_directory))
			Directory.Delete(_directory, recursive: true);
	}

	private FootprintTapeRepository Create() => new(_directory);

	private static FootprintTapeTradeDto Trade(
		int second,
		double price,
		double size = 100,
		FootprintSide side = FootprintSide.Unknown
	) => new(T0.AddSeconds(second).ToUnixTimeSeconds(), price, size, side);

	private string DayFile(DateTimeOffset day) =>
		Path.Combine(
			_directory,
			ConId.ToString(),
			day.UtcDateTime.ToString("yyyy-MM-dd") + FootprintConstants.TapeFileExtension
		);

	private static DateTimeOffset Day(DateTimeOffset time) =>
		new(time.UtcDateTime.Date, TimeSpan.Zero);

	private IReadOnlyList<FootprintTapeRangeDto> GetDay(FootprintTapeRepository repository) =>
		repository.Get(ConId, Day(T0), Day(T0).AddDays(1));

	[Fact]
	public void Trades_round_trip_with_their_prices_sizes_and_quote_sides()
	{
		FootprintTapeTradeDto[] trades =
		[
			Trade(0, 512.34, 100, FootprintSide.Ask),
			Trade(0, 512.3412, 1),
			Trade(3, 512.33, 0.25, FootprintSide.Bid),
			Trade(3, 498.10, 250_000),
		];
		Create().Append(ConId, T0, T0.AddSeconds(10), trades);

		// A new instance reads the file from disk
		var range = Assert.Single(GetDay(Create()));

		Assert.Equal((T0, T0.AddSeconds(10)), (range.From, range.To));
		Assert.Equal(trades, range.Trades);
	}

	[Fact]
	public void What_is_already_recorded_is_kept_and_only_the_rest_appended()
	{
		var repository = Create();
		repository.Append(ConId, T0.AddSeconds(10), T0.AddSeconds(20), [Trade(15, 10)]);

		// A page reaching over the recorded stretch: only 0–10 and 20–30 are new
		repository.Append(
			ConId,
			T0,
			T0.AddSeconds(30),
			[Trade(5, 9), Trade(15, 99), Trade(25, 11)]
		);

		var range = Assert.Single(GetDay(Create()));
		Assert.Equal((T0, T0.AddSeconds(30)), (range.From, range.To));
		Assert.Equal([9, 10, 11], range.Trades.Select(x => x.Price));
	}

	[Fact]
	public void Stretches_with_a_gap_between_come_back_apart()
	{
		var repository = Create();
		repository.Append(ConId, T0, T0.AddSeconds(10), [Trade(1, 10)]);
		repository.Append(ConId, T0.AddSeconds(20), T0.AddSeconds(30), [Trade(21, 11)]);

		var ranges = GetDay(repository);

		Assert.Equal(
			[(T0, T0.AddSeconds(10)), (T0.AddSeconds(20), T0.AddSeconds(30))],
			ranges.Select(x => (x.From, x.To))
		);
	}

	[Fact]
	public void A_stretch_over_midnight_goes_to_both_days_and_comes_back_whole()
	{
		var midnight = Day(T0).AddDays(1);
		var before = midnight.AddSeconds(-5).ToUnixTimeSeconds();
		var after = midnight.AddSeconds(5).ToUnixTimeSeconds();
		var repository = Create();

		repository.Append(
			ConId,
			midnight.AddMinutes(-1),
			midnight.AddMinutes(1),
			[new(before, 10, 1, FootprintSide.Unknown), new(after, 11, 1, FootprintSide.Unknown)]
		);

		Assert.True(File.Exists(DayFile(T0)));
		Assert.True(File.Exists(DayFile(midnight)));
		var range = Assert.Single(Create().Get(ConId, midnight.AddDays(-1), midnight.AddDays(1)));
		Assert.Equal((midnight.AddMinutes(-1), midnight.AddMinutes(1)), (range.From, range.To));
		Assert.Equal([10, 11], range.Trades.Select(x => x.Price));
	}

	// A crash mid-write: the stretch whose frame is cut short is not covered, and later appends
	// go after the last whole frame
	[Fact]
	public void A_torn_tail_is_ignored_and_cut_off_before_the_next_append()
	{
		Create().Append(ConId, T0, T0.AddSeconds(10), [Trade(1, 10)]);
		Create().Append(ConId, T0.AddSeconds(10), T0.AddSeconds(20), [Trade(11, 11)]);
		var path = DayFile(T0);
		var whole = new FileInfo(path).Length;
		using (var stream = new FileStream(path, FileMode.Open))
			stream.SetLength(whole - 3);

		var repository = Create();
		var range = Assert.Single(GetDay(repository));
		Assert.Equal((T0, T0.AddSeconds(10)), (range.From, range.To));

		repository.Append(ConId, T0.AddSeconds(10), T0.AddSeconds(20), [Trade(12, 12)]);
		var after = Assert.Single(GetDay(Create()));
		Assert.Equal((T0, T0.AddSeconds(20)), (after.From, after.To));
		Assert.Equal([10, 12], after.Trades.Select(x => x.Price));
	}

	[Fact]
	public void A_corrupt_frame_is_dropped_with_everything_after_it()
	{
		Create().Append(ConId, T0, T0.AddSeconds(10), [Trade(1, 10)]);
		var firstEnd = new FileInfo(DayFile(T0)).Length;
		Create().Append(ConId, T0.AddSeconds(10), T0.AddSeconds(20), [Trade(11, 11)]);
		Create().Append(ConId, T0.AddSeconds(20), T0.AddSeconds(30), [Trade(21, 12)]);
		var bytes = File.ReadAllBytes(DayFile(T0));
		bytes[firstEnd + 10] ^= 0xFF;
		File.WriteAllBytes(DayFile(T0), bytes);

		var range = Assert.Single(GetDay(Create()));

		Assert.Equal((T0, T0.AddSeconds(10)), (range.From, range.To));
		Assert.Equal([10], range.Trades.Select(x => x.Price));
	}

	[Fact]
	public void A_file_that_is_not_a_cache_file_holds_nothing_and_is_rewritten()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(DayFile(T0))!);
		File.WriteAllText(DayFile(T0), "not a footprint file");
		var repository = Create();

		Assert.Empty(GetDay(repository));
		repository.Append(ConId, T0, T0.AddSeconds(10), [Trade(1, 10)]);

		Assert.Equal([10], Assert.Single(GetDay(Create())).Trades.Select(x => x.Price));
	}

	[Fact]
	public void Pruning_deletes_old_days_then_the_oldest_until_it_fits()
	{
		var repository = Create();
		for (var day = 0; day < 4; day++)
			repository.Append(
				ConId,
				T0.AddDays(-day),
				T0.AddDays(-day).AddSeconds(10),
				[Trade(1 - day * 86_400, 10)]
			);
		var oneDay = new FileInfo(DayFile(T0)).Length;

		// Days before T0 - 2 go by age; of the three left, the oldest goes by size
		repository.Prune(DateOnly.FromDateTime(T0.AddDays(-2).UtcDateTime), oneDay * 2);

		Assert.False(File.Exists(DayFile(T0.AddDays(-3))));
		Assert.False(File.Exists(DayFile(T0.AddDays(-2))));
		Assert.True(File.Exists(DayFile(T0.AddDays(-1))));
		Assert.True(File.Exists(DayFile(T0)));
		Assert.Equal(oneDay * 2, repository.GetSizeBytes());
		Assert.Empty(repository.Get(ConId, Day(T0).AddDays(-2), Day(T0).AddDays(-1)));
	}
}
