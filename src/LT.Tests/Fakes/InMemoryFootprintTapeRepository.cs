using LT.Contracts.Dtos.Charts;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Tests.Fakes;

/// <summary>The footprint trade cache in memory: ranges per contract, parts already in kept.</summary>
public class InMemoryFootprintTapeRepository : IFootprintTapeRepository
{
	private readonly Lock _lock = new();
	private readonly Dictionary<int, List<FootprintTapeRangeDto>> _ranges = [];

	public int Appends { get; private set; }

	public void Append(
		int conId,
		DateTimeOffset from,
		DateTimeOffset to,
		IReadOnlyList<FootprintTapeTradeDto> trades
	)
	{
		lock (_lock)
		{
			Appends++;
			if (!_ranges.TryGetValue(conId, out var ranges))
				_ranges[conId] = ranges = [];
			var sorted = trades.OrderBy(x => x.UnixSeconds).ToList();
			foreach (
				var (gapFrom, gapTo) in FootprintTapeHelpers.GetGaps(
					ranges.Select(x => (x.From, x.To)).ToList(),
					from,
					to
				)
			)
			{
				ranges.Add(
					new FootprintTapeRangeDto
					{
						From = gapFrom,
						To = gapTo,
						Trades = sorted.Where(x => x.Time >= gapFrom && x.Time < gapTo).ToList(),
					}
				);
				ranges.Sort((a, b) => a.From.CompareTo(b.From));
			}
		}
	}

	public IReadOnlyList<FootprintTapeRangeDto> Get(
		int conId,
		DateTimeOffset from,
		DateTimeOffset to
	)
	{
		lock (_lock)
		{
			var result = new List<FootprintTapeRangeDto>();
			foreach (var range in _ranges.GetValueOrDefault(conId) ?? [])
			{
				if (range.To <= from || range.From >= to)
					continue;
				if (
					result.Count > 0
					&& result[^1].From <= range.From
					&& result[^1].To == range.From
				)
					result[^1] = new FootprintTapeRangeDto
					{
						From = result[^1].From,
						To = range.To,
						Trades = [.. result[^1].Trades, .. range.Trades],
					};
				else
					result.Add(range);
			}
			return result;
		}
	}

	public void Prune(DateOnly keepFrom, long maxBytes) { }

	public long GetSizeBytes() => 0;
}
