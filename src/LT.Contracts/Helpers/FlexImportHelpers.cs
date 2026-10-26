using LT.Contracts.Constants;
using LT.Contracts.Entities;

namespace LT.Contracts.Helpers;

/// <summary>
/// When the Flex import runs and which date ranges it asks for. The Activity statement covers
/// through the previous business day, so every range ends yesterday (New York date); each spans at
/// most <see cref="FlexConstants.MaxDaysPerRequest"/> days (IB's fd / td limit).
/// </summary>
public static class FlexImportHelpers
{
	/// <summary>The newest year, asked first on a fresh configuration so recent history shows quickly.</summary>
	public static (DateOnly From, DateOnly To) GetInitialRange(DateOnly today)
	{
		var to = today.AddDays(-1);
		return (to.AddDays(-(FlexConstants.MaxDaysPerRequest - 1)), to);
	}

	/// <summary>
	/// The ranges from a few days before the last covered day up to yesterday, oldest first, in
	/// chunks of at most a year (more than one after the app hasn't run for a long time). Empty
	/// when yesterday is covered already.
	/// </summary>
	public static IReadOnlyList<(DateOnly From, DateOnly To)> GetForwardRanges(
		DateOnly coveredTo,
		DateOnly today
	)
	{
		var yesterday = today.AddDays(-1);
		var ranges = new List<(DateOnly, DateOnly)>();
		var from = coveredTo.AddDays(-FlexConstants.IncrementalOverlapDays);
		while (from <= yesterday && ranges.Count < FlexConstants.MaxBackfillChunks)
		{
			var to = Min(from.AddDays(FlexConstants.MaxDaysPerRequest - 1), yesterday);
			ranges.Add((from, to));
			from = to.AddDays(1);
		}
		return ranges;
	}

	/// <summary>The year before <paramref name="coveredFrom"/>, for the back-fill.</summary>
	public static (DateOnly From, DateOnly To) GetBackfillRange(DateOnly coveredFrom)
	{
		var to = coveredFrom.AddDays(-1);
		return (to.AddDays(-(FlexConstants.MaxDaysPerRequest - 1)), to);
	}

	/// <summary>
	/// The daily Activity import is due when none succeeded today (New York date) and the last
	/// attempt didn't fail within <see cref="FlexConstants.ActivityRetryInterval"/>.
	/// </summary>
	public static bool IsActivityDue(
		FlexImportStateEntity state,
		DateTimeOffset now,
		TimeZoneInfo zone
	)
	{
		if (SucceededToday(state, now, zone))
			return false;
		return !FailedRecently(state.LastActivityAttempt, state.LastActivitySuccess, now);
	}

	/// <summary>An Activity import succeeded on today's New York date.</summary>
	public static bool SucceededToday(
		FlexImportStateEntity state,
		DateTimeOffset now,
		TimeZoneInfo zone
	) =>
		state.LastActivitySuccess is { } success
		&& TradeJournalHelpers.GetTradeDay(success, zone)
			>= TradeJournalHelpers.GetTradeDay(now, zone);

	/// <summary>"Importing account history 2025-10-27 – 2026-10-25" (status line).</summary>
	public static string FormatRange(string what, DateOnly from, DateOnly to) =>
		string.Create(
			System.Globalization.CultureInfo.InvariantCulture,
			$"{what} {from:yyyy-MM-dd} – {to:yyyy-MM-dd}"
		);

	/// <summary>Back-fill continues until complete; after a failure it waits the retry interval.</summary>
	public static bool IsBackfillDue(FlexImportStateEntity state, DateTimeOffset now) =>
		state.CoveredFrom != null
		&& !state.BackfillComplete
		&& !FailedRecently(state.LastActivityAttempt, state.LastActivitySuccess, now);

	public static bool IsTradeConfirmDue(FlexImportStateEntity state, DateTimeOffset now) =>
		state.LastTradeConfirmAttempt is not { } attempt
		|| now - attempt >= FlexConstants.TradeConfirmInterval;

	/// <summary>How long the scheduler may sleep before something is due (zero: now).</summary>
	public static TimeSpan GetDelayUntilDue(
		FlexImportStateEntity state,
		bool hasTradeConfirmQuery,
		DateTimeOffset now,
		TimeZoneInfo zone
	)
	{
		if (IsActivityDue(state, now, zone) || IsBackfillDue(state, now))
			return TimeSpan.Zero;

		// Next New York midnight, or the end of the retry wait after a failure
		var nextActivity = FailedRecently(state.LastActivityAttempt, state.LastActivitySuccess, now)
			? state.LastActivityAttempt!.Value + FlexConstants.ActivityRetryInterval
			: GetNextMidnight(now, zone);
		var due = nextActivity;
		if (hasTradeConfirmQuery)
		{
			var nextTradeConfirm =
				(state.LastTradeConfirmAttempt ?? now) + FlexConstants.TradeConfirmInterval;
			if (nextTradeConfirm < due)
				due = nextTradeConfirm;
		}
		return due > now ? due - now : TimeSpan.Zero;
	}

	/// <summary>Moves the covered range out to include <paramref name="from"/>–<paramref name="to"/>.</summary>
	public static void ExtendCoverage(FlexImportStateEntity state, DateOnly from, DateOnly to)
	{
		state.CoveredFrom = state.CoveredFrom is { } coveredFrom ? Min(coveredFrom, from) : from;
		state.CoveredTo = state.CoveredTo is { } coveredTo ? Max(coveredTo, to) : to;
	}

	/// <summary>
	/// Notes whether an Activity statement for <paramref name="from"/>–<paramref name="to"/> had
	/// the Cash Transactions / Transfers sections. When one appears that the history imported so
	/// far didn't have (the user added it to the query, or the state predates tracking it), the
	/// history starts over from this statement's range — the newer years again with the next
	/// daily run, the older ones with the back-fill — so every year's movements come in (merges
	/// are idempotent). Returns true when it started over.
	/// </summary>
	public static bool NoteCashSections(
		FlexImportStateEntity state,
		bool hasCashTransactions,
		bool hasTransfers,
		DateOnly from,
		DateOnly to
	)
	{
		var added =
			(hasCashTransactions && state.HadCashTransactionsSection != true)
			|| (hasTransfers && state.HadTransfersSection != true);
		state.HadCashTransactionsSection = hasCashTransactions;
		state.HadTransfersSection = hasTransfers;
		if (!added || state.CoveredFrom == null)
			return false;
		state.CoveredFrom = from;
		state.CoveredTo = to;
		state.BackfillComplete = false;
		// Due again right away: the newer years come in with the next run
		state.LastActivitySuccess = null;
		state.LastActivityAttempt = null;
		return true;
	}

	/// <summary>
	/// The last Activity statement lacked Cash Transactions or Transfers and came at least
	/// <see cref="FlexConstants.MissingSectionRecheckInterval"/> ago: F5 fetches it again even
	/// after today's import, so a section just added to the query shows without waiting for
	/// tomorrow.
	/// </summary>
	public static bool ShouldRecheckCashSections(FlexImportStateEntity state, DateTimeOffset now) =>
		(state.HadCashTransactionsSection != true || state.HadTransfersSection != true)
		&& (
			state.LastActivitySuccess is not { } success
			|| now - success >= FlexConstants.MissingSectionRecheckInterval
		);

	private static bool FailedRecently(
		DateTimeOffset? attempt,
		DateTimeOffset? success,
		DateTimeOffset now
	) =>
		attempt is { } tried
		&& (success is not { } done || done < tried)
		&& now - tried < FlexConstants.ActivityRetryInterval;

	private static DateTimeOffset GetNextMidnight(DateTimeOffset now, TimeZoneInfo zone)
	{
		var tomorrow = TradeJournalHelpers.GetTradeDay(now, zone).AddDays(1).ToDateTime(default);
		return new DateTimeOffset(tomorrow, zone.GetUtcOffset(tomorrow));
	}

	private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

	private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}
