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
	/// Adds <paramref name="incoming"/> to <paramref name="fills"/> unless the same fill is there —
	/// same ExecId (IB's execution id, which the socket API and Flex share), or same Flex TradeId;
	/// then fills in what arrived later (IB sends the commission report after the execution; a
	/// Flex fill brings the tradeID, multiplier and final commission). Returns whether anything
	/// changed. Fills without an ExecId are skipped.
	/// </summary>
	public static bool MergeFill(List<FillEntity> fills, FillEntity incoming)
	{
		if (string.IsNullOrEmpty(incoming.ExecId))
			return false;

		var existing = fills.Find(x =>
			x.ExecId == incoming.ExecId
			|| (incoming.TradeId.Length > 0 && x.TradeId == incoming.TradeId)
		);
		if (existing == null)
		{
			fills.Add(incoming);
			return true;
		}

		var changed = false;
		// A socket fill matched by a Flex one keeps its id; a "flex:" key gives way to the real id
		if (
			existing.ExecId.StartsWith(FlexConstants.TradeIdExecIdPrefix, StringComparison.Ordinal)
			&& !incoming.ExecId.StartsWith(
				FlexConstants.TradeIdExecIdPrefix,
				StringComparison.Ordinal
			)
		)
		{
			existing.ExecId = incoming.ExecId;
			changed = true;
		}
		if (incoming.TradeId.Length > 0 && existing.TradeId != incoming.TradeId)
		{
			existing.TradeId = incoming.TradeId;
			changed = true;
		}
		if (existing.Multiplier is null && incoming.Multiplier is { } multiplier)
		{
			existing.Multiplier = multiplier;
			changed = true;
		}
		if (existing.ConId == 0 && incoming.ConId != 0)
		{
			existing.ConId = incoming.ConId;
			changed = true;
		}
		if (existing.LocalSymbol.Length == 0 && incoming.LocalSymbol.Length > 0)
		{
			existing.LocalSymbol = incoming.LocalSymbol;
			changed = true;
		}
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

	/// <summary>16:00 New York on <paramref name="day"/>: where a fill without a time is placed.</summary>
	public static DateTimeOffset GetSessionClose(DateOnly day, TimeZoneInfo zone)
	{
		var close = day.ToDateTime(new TimeOnly(16, 0));
		return new DateTimeOffset(close, zone.GetUtcOffset(close));
	}

	/// <summary>Removes the fills whose Flex TradeId is in <paramref name="tradeIds"/>; returns how many.</summary>
	public static int RemoveTrades(List<FillEntity> fills, IReadOnlyCollection<string> tradeIds) =>
		tradeIds.Count == 0
			? 0
			: fills.RemoveAll(x => x.TradeId.Length > 0 && tradeIds.Contains(x.TradeId));

	/// <summary>The recorded day before <paramref name="day"/>, or null.</summary>
	public static DateOnly? GetPreviousDay(IEnumerable<DateOnly> days, DateOnly day) =>
		days.Where(x => x < day).Select(x => (DateOnly?)x).Max();

	/// <summary>The recorded day after <paramref name="day"/>, or null.</summary>
	public static DateOnly? GetNextDay(IEnumerable<DateOnly> days, DateOnly day) =>
		days.Where(x => x > day).Select(x => (DateOnly?)x).Min();
}
