using System.Globalization;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Flex;

namespace LT.App.Helpers;

/// <summary>The Flex import status as short text for the Settings status line and the widgets' notes.</summary>
public static class FlexStatusHelpers
{
	/// <summary>
	/// "Running: …", or the last imports and the covered history, plus the last error. Times are
	/// in <paramref name="zone"/> (the display zone); <paramref name="now"/> decides "today".
	/// </summary>
	public static string Describe(FlexImportStatusDto status, TimeZoneInfo zone, DateTimeOffset now)
	{
		if (!status.IsConfigured)
			return "Not set up: enter the token and the Activity query ID.";
		if (status.Activity is { } activity)
			return $"{activity}…";

		var parts = new List<string>();
		if (status.CoveredFrom is { } from && status.CoveredTo is { } to)
			parts.Add(
				$"History {Date(from)} – {Date(to)}"
					+ (status.BackfillComplete ? "" : " (older years still to come)")
			);
		parts.Add(
			status.LastActivityImport is { } activityImport
				? $"daily import {Time(activityImport, zone, now)}"
				: "no import yet"
		);
		if (status.HasTradeConfirmQuery)
			parts.Add(
				status.LastTradeConfirmImport is { } trades
					? $"today's trades {Time(trades, zone, now)}"
					: "today's trades not imported yet"
			);
		var text = string.Join(" · ", parts);
		return status.LastError is { } error
			? $"{text}\nLast error{(status.LastErrorAt is { } at ? $" ({Time(at, zone, now)})" : "")}: {error}"
			: text;
	}

	/// <summary>
	/// The Day trade widget's note on where today's trades come from; null when Flex isn't set
	/// up. The last import's time is in <paramref name="zone"/> (the display zone).
	/// </summary>
	public static string? DescribeTodayTrades(
		FlexImportStatusDto status,
		TimeZoneInfo zone,
		DateTimeOffset now
	) =>
		!status.IsConfigured ? null
		: !status.HasTradeConfirmQuery
			? "Trades come from IB Flex through yesterday; a Trade Confirmation query (Settings → Account history) adds today's."
		: status.LastTradeConfirmImport is { } at
			? $"Today's trades via IB Flex, up to ~{(int)FlexConstants.TradeConfirmInterval.TotalMinutes} min delayed (last import {Time(at, zone, now)})."
		: "Today's trades via IB Flex, not imported yet.";

	/// <summary>
	/// The setup banner of the performance widgets: shown while Flex isn't configured, and in the
	/// warning style for an error only the user can fix (expired / invalid token, bad query id, IP
	/// restriction, a query missing a section). Null when nothing needs the user.
	/// </summary>
	public static FlexBannerState? GetBanner(FlexImportStatusDto status) =>
		!status.IsConfigured
			? new FlexBannerState(
				"History needs IB Flex — set it up once in Settings.",
				IsWarning: false
			)
		: status.ErrorNeedsUser && status.LastError is { } error
			? new FlexBannerState($"IB Flex import failed: {error}", IsWarning: true)
		: null;

	/// <summary>
	/// The note next to Account performance's "Deposits &amp; transfers" check box, so it never
	/// shows nothing silently: what to add to the Activity Flex query when a section is missing,
	/// that none were found, or that the import hasn't run. Null when movements are shown and
	/// both sections are there.
	/// </summary>
	public static string? GetCashMovementsHint(FlexImportStatusDto status, bool hasMovements)
	{
		if (!status.IsConfigured)
			return "Deposits and transfers come from IB Flex — set it up in Settings.";
		if (status.HasCashTransactionsSection == null && status.HasTransfersSection == null)
			return hasMovements ? null : "Waiting for the IB Flex import.";
		var missing = new List<string>();
		if (status.HasCashTransactionsSection != true)
			missing.Add("Cash Transactions (option Deposits & Withdrawals)");
		if (status.HasTransfersSection != true)
			missing.Add("Transfers");
		if (missing.Count > 0)
			return $"Add {string.Join(" and ", missing)} to your Activity Flex query in Client Portal, then press F5.";
		if (!hasMovements && (status.Activity != null || !status.BackfillComplete))
			return "Importing deposits and transfers…";
		return hasMovements
			? null
			: "No deposits, withdrawals or transfers in the imported history.";
	}

	private static string Date(DateOnly date) =>
		date.ToString("d MMM yyyy", CultureInfo.CurrentCulture);

	private static string Time(DateTimeOffset time, TimeZoneInfo zone, DateTimeOffset now) =>
		FormatHelpers.DayTime(time, now, zone);
}

/// <summary>What the Flex setup banner says; <see cref="IsWarning"/> for an error rather than a missing setup.</summary>
public sealed record FlexBannerState(string Text, bool IsWarning);
