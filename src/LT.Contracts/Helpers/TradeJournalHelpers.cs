using LT.Contracts.Constants;
using LT.Contracts.Entities;

namespace LT.Contracts.Helpers;

public static class TradeJournalHelpers
{
	/// <summary>The zone trading days are counted in (<see cref="TradeJournalConstants.TradeDayTimeZoneId"/>).</summary>
	public static TimeZoneInfo GetTradeDayTimeZone() =>
		TimeZoneInfo.TryFindSystemTimeZoneById(
			TradeJournalConstants.TradeDayTimeZoneId,
			out var zone
		)
			? zone
			: TimeZoneInfo.Local;

	/// <summary>The trading day <paramref name="time"/> falls on, in <paramref name="zone"/>.</summary>
	public static DateOnly GetTradeDay(DateTimeOffset time, TimeZoneInfo zone) =>
		DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time, zone).DateTime);

	/// <summary>
	/// Adds <paramref name="incoming"/> to <paramref name="fills"/> unless a fill with its ExecId is
	/// there; then fills in what arrived later (IB sends the commission report after the
	/// execution). Returns whether anything changed. Fills without an ExecId are skipped.
	/// </summary>
	public static bool MergeFill(List<FillEntity> fills, FillEntity incoming)
	{
		if (string.IsNullOrEmpty(incoming.ExecId))
			return false;

		var existing = fills.Find(x => x.ExecId == incoming.ExecId);
		if (existing == null)
		{
			fills.Add(incoming);
			return true;
		}

		var changed = false;
		if (incoming.Commission is { } commission && existing.Commission != commission)
		{
			existing.Commission = commission;
			existing.CommissionCurrency = incoming.CommissionCurrency;
			changed = true;
		}
		if (incoming.RealizedPnl is { } realized && existing.RealizedPnl != realized)
		{
			existing.RealizedPnl = realized;
			changed = true;
		}
		return changed;
	}

	/// <summary>The recorded day before <paramref name="day"/>, or null.</summary>
	public static DateOnly? GetPreviousDay(IEnumerable<DateOnly> days, DateOnly day) =>
		days.Where(x => x < day).Select(x => (DateOnly?)x).Max();

	/// <summary>The recorded day after <paramref name="day"/>, or null.</summary>
	public static DateOnly? GetNextDay(IEnumerable<DateOnly> days, DateOnly day) =>
		days.Where(x => x > day).Select(x => (DateOnly?)x).Min();
}
