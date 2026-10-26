using LT.Contracts.Entities;

namespace LT.Contracts.Interfaces.IRepositories;

/// <summary>The local trade journal: fills and intraday P&amp;L samples, one file per trading day.</summary>
public interface ITradeJournalRepository
{
	IReadOnlyList<DateOnly> GetFillDays();
	IReadOnlyList<FillEntity> GetFills(DateOnly day);
	void SaveFills(DateOnly day, IReadOnlyList<FillEntity> fills);

	IReadOnlyList<DateOnly> GetPnlDays();
	IReadOnlyList<PnlSampleEntity> GetPnlSamples(DateOnly day);
	void SavePnlSamples(DateOnly day, IReadOnlyList<PnlSampleEntity> samples);
}
