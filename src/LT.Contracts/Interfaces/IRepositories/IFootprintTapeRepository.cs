using LT.Contracts.Dtos.Charts;

namespace LT.Contracts.Interfaces.IRepositories;

/// <summary>
/// The footprint trade cache: every trade of the stretches of time recorded, per contract, one
/// file per UTC day, so a chart's footprints come back from disk and IB is asked only for what
/// is missing. A cache: unreadable files or frames are missing data, failed writes are dropped.
/// Thread-safe.
/// </summary>
public interface IFootprintTapeRepository
{
	/// <summary>
	/// Records that <paramref name="trades"/> are every trade of <paramref name="from"/> to before
	/// <paramref name="to"/> (trades outside it are left out). Parts already recorded are kept as
	/// they are; a range is only ever marked recorded together with its trades.
	/// </summary>
	void Append(
		int conId,
		DateTimeOffset from,
		DateTimeOffset to,
		IReadOnlyList<FootprintTapeTradeDto> trades
	);

	/// <summary>
	/// The recorded ranges of the UTC days from <paramref name="from"/> to before
	/// <paramref name="to"/>, oldest first, touching ones merged.
	/// </summary>
	IReadOnlyList<FootprintTapeRangeDto> Get(int conId, DateTimeOffset from, DateTimeOffset to);

	/// <summary>
	/// Deletes the days before <paramref name="keepFrom"/>, then the oldest days until all of it
	/// fits <paramref name="maxBytes"/>.
	/// </summary>
	void Prune(DateOnly keepFrom, long maxBytes);

	/// <summary>Total size of the files, in bytes.</summary>
	long GetSizeBytes();
}
