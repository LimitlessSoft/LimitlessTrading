using LT.Contracts.Entities;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Tests.Fakes;

public class InMemoryTradeJournalRepository : ITradeJournalRepository
{
	public Dictionary<DateOnly, List<FillEntity>> Fills { get; } = [];
	public Dictionary<DateOnly, List<PnlSampleEntity>> PnlSamples { get; } = [];
	public int PnlSaves { get; private set; }

	public IReadOnlyList<DateOnly> GetFillDays() => Fills.Keys.Order().ToList();

	public IReadOnlyList<FillEntity> GetFills(DateOnly day) => Fills.GetValueOrDefault(day, []);

	public void SaveFills(DateOnly day, IReadOnlyList<FillEntity> fills) =>
		Fills[day] = fills.ToList();

	public IReadOnlyList<DateOnly> GetPnlDays() => PnlSamples.Keys.Order().ToList();

	public IReadOnlyList<PnlSampleEntity> GetPnlSamples(DateOnly day) =>
		PnlSamples.GetValueOrDefault(day, []);

	public void SavePnlSamples(DateOnly day, IReadOnlyList<PnlSampleEntity> samples)
	{
		PnlSaves++;
		PnlSamples[day] = samples
			.Select(x => new PnlSampleEntity
			{
				Account = x.Account,
				Time = x.Time,
				DailyPnl = x.DailyPnl,
			})
			.ToList();
	}
}
