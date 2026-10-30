using System.Globalization;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>
/// Earnings results from several sources folded into one per symbol and day, which rows still
/// want one, and when the calendar asks again.
/// </summary>
public static class EarningsResultHelpers
{
	/// <summary>
	/// One result per symbol and day from the sources' answers, given fastest source first: the
	/// first result with figures wins (a later source's number never replaces it), and a result
	/// that only says the report is out (an 8-K) gives way to one with figures but lends it its
	/// filing link and the earliest report time.
	/// </summary>
	public static IReadOnlyList<EarningsResultDto> Merge(
		IEnumerable<IEnumerable<EarningsResultDto>> answers
	)
	{
		var merged = new Dictionary<(string Symbol, DateOnly Date), EarningsResultDto>();
		var order = new List<(string Symbol, DateOnly Date)>();
		foreach (var answer in answers)
		foreach (var result in answer)
		{
			if (result.Symbol.Length == 0)
				continue;
			var key = (GetSymbolKey(result.Symbol), result.Date);
			if (merged.TryGetValue(key, out var kept))
				merged[key] = Combine(kept, result);
			else
			{
				merged[key] = Copy(result);
				order.Add(key);
			}
		}
		return [.. order.Select(x => merged[x])];
	}

	/// <summary>
	/// What is known of a result once <paramref name="incoming"/> arrives after
	/// <paramref name="kept"/>: the first figures stay, the filing link and the earliest report
	/// time are kept from either. Neither argument is changed.
	/// </summary>
	public static EarningsResultDto Combine(EarningsResultDto? kept, EarningsResultDto incoming)
	{
		if (kept == null)
			return Copy(incoming);
		var winner = kept.HasFigures || !incoming.HasFigures ? kept : incoming;
		var other = winner == kept ? incoming : kept;
		var combined = Copy(winner);
		combined.Symbol = combined.Symbol.ToUpperInvariant();
		combined.FilingUrl ??= other.FilingUrl;
		if (other.ReportedAt is { } at && (combined.ReportedAt is not { } mine || at < mine))
			combined.ReportedAt = at;
		return combined;
	}

	/// <summary>An earnings row that still wants figures: no reported EPS from its source or a result.</summary>
	public static bool NeedsResult(CalendarEventDto item, EarningsResultDto? known) =>
		item.Kind == CalendarEventKind.Earnings
		&& item.Symbol.Length > 0
		&& item.EpsActual is null
		&& known?.HasFigures != true;

	/// <summary>
	/// The days whose rows are looked up: today and the <see cref="EarningsResultConstants.LookbackDays"/>
	/// before it that fall in the shown range (later days have no results yet).
	/// </summary>
	public static IReadOnlyList<DateOnly> GetLookupDays(
		DateOnly start,
		DateOnly end,
		DateOnly today
	)
	{
		var days = new List<DateOnly>();
		for (
			var day = today.AddDays(-EarningsResultConstants.LookbackDays);
			day <= today;
			day = day.AddDays(1)
		)
			if (day >= start && day <= end)
				days.Add(day);
		return days;
	}

	/// <summary>
	/// The surprise to show: the source's, else worked out from the estimate (rounded to two
	/// decimals, against the estimate's size so a smaller loss than expected is a beat); null
	/// without a result or an estimate.
	/// </summary>
	public static decimal? GetSurprisePercent(decimal? estimate, EarningsResultDto? result)
	{
		if (result?.EpsActual is not { } actual)
			return null;
		if (result.EpsSurprisePercent is { } given)
			return given;
		if (estimate is not { } expected || expected == 0)
			return null;
		return Math.Round((actual - expected) / Math.Abs(expected) * 100, 2);
	}

	/// <summary>
	/// How long until the calendar asks again for today's results after <paramref name="now"/>
	/// (<paramref name="zone"/> New York): <paramref name="interval"/> (at least
	/// <see cref="EarningsResultConstants.MinPollInterval"/>) between
	/// <see cref="EarningsResultConstants.PollStart"/> and
	/// <see cref="EarningsResultConstants.PollEnd"/>, until the start before it, null after the
	/// end (no more polling today).
	/// </summary>
	public static TimeSpan? GetPollDelay(DateTimeOffset now, TimeSpan interval, TimeZoneInfo zone)
	{
		var local = TimeZoneInfo.ConvertTime(now, zone);
		var time = TimeOnly.FromDateTime(local.DateTime);
		if (time >= EarningsResultConstants.PollEnd)
			return null;
		if (time < EarningsResultConstants.PollStart)
			return EarningsResultConstants.PollStart - time;
		var delay =
			interval < EarningsResultConstants.MinPollInterval
				? EarningsResultConstants.MinPollInterval
				: interval;
		var end = EarningsResultConstants.PollEnd - time;
		return delay < end ? delay : end;
	}

	/// <summary>SEC EDGAR's interval at <paramref name="now"/>: short in the windows when releases go out.</summary>
	public static TimeSpan GetSecInterval(DateTimeOffset now, TimeZoneInfo zone)
	{
		var time = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
		return EarningsResultConstants.SecFastWindows.Any(x => time >= x.Start && time < x.End)
			? EarningsResultConstants.SecFastInterval
			: EarningsResultConstants.SecInterval;
	}

	/// <summary>
	/// The tooltip's part about the result: "EPS from Yahoo Finance · 8-K filed 07:03 (SEC
	/// EDGAR)"; empty without one. <paramref name="formatTime"/> shows a time in the user's zone.
	/// </summary>
	public static string Describe(
		EarningsResultDto? result,
		Func<DateTimeOffset, string> formatTime
	)
	{
		if (result == null)
			return string.Empty;
		var parts = new List<string>();
		if (result.HasFigures)
			parts.Add("EPS from " + result.Source);
		if (result.FilingUrl != null)
			parts.Add(
				result.ReportedAt is { } at
					? $"8-K filed {formatTime(at)} (SEC EDGAR)"
					: "8-K filed (SEC EDGAR)"
			);
		else if (!result.HasFigures)
			parts.Add("Reported (" + result.Source + ")");
		return string.Join(" · ", parts);
	}

	/// <summary>
	/// The reported EPS came in or changed: <paramref name="current"/> is a figure and differs
	/// from <paramref name="previous"/> (a figure going away is not news).
	/// </summary>
	public static bool IsActualUpdate(decimal? previous, decimal? current) =>
		current is not null && current != previous;

	/// <summary>
	/// A row whose reported EPS came in at <paramref name="updatedAt"/> is still highlighted at
	/// <paramref name="now"/>: for <see cref="EarningsResultConstants.ActualHighlightDuration"/>.
	/// </summary>
	public static bool IsActualFresh(DateTimeOffset? updatedAt, DateTimeOffset now) =>
		updatedAt is { } at
		&& now >= at
		&& now - at < EarningsResultConstants.ActualHighlightDuration;

	/// <summary>
	/// When the next highlight of <paramref name="updates"/> (the times the rows' EPS came in)
	/// ends after <paramref name="now"/>; null when none is still on.
	/// </summary>
	public static DateTimeOffset? GetNextHighlightEnd(
		IEnumerable<DateTimeOffset> updates,
		DateTimeOffset now
	)
	{
		DateTimeOffset? next = null;
		foreach (var at in updates)
		{
			if (!IsActualFresh(at, now))
				continue;
			var end = at + EarningsResultConstants.ActualHighlightDuration;
			if (next is not { } kept || end < kept)
				next = end;
		}
		return next;
	}

	/// <summary>
	/// What a symbol is matched by across sources, which write share classes differently
	/// (Nasdaq "BRK/B", Yahoo and the SEC "BRK-B", others "BRK.B"): upper case, one separator.
	/// </summary>
	public static string GetSymbolKey(string symbol) =>
		symbol.Trim().ToUpperInvariant().Replace('/', '-').Replace('.', '-');

	/// <summary>A day as the sources' URLs want it: 2026-10-29.</summary>
	public static string FormatDay(DateOnly day) =>
		day.ToString(YahooConstants.DateFormat, CultureInfo.InvariantCulture);

	private static EarningsResultDto Copy(EarningsResultDto result) =>
		new()
		{
			Symbol = result.Symbol.ToUpperInvariant(),
			Date = result.Date,
			EpsActual = result.EpsActual,
			EpsSurprisePercent = result.EpsSurprisePercent,
			ReportedAt = result.ReportedAt,
			Source = result.Source,
			FilingUrl = result.FilingUrl,
		};
}
