using System.Globalization;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Flex;

namespace LT.App.Helpers;

/// <summary>The Flex import status as short text for the Settings status line and the widgets' notes.</summary>
public static class FlexStatusHelpers
{
	/// <summary>"Running: …", or the last imports and the covered history, plus the last error.</summary>
	public static string Describe(FlexImportStatusDto status)
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
				? $"daily import {Time(activityImport)}"
				: "no import yet"
		);
		if (status.HasTradeConfirmQuery)
			parts.Add(
				status.LastTradeConfirmImport is { } trades
					? $"today's trades {Time(trades)}"
					: "today's trades not imported yet"
			);
		var text = string.Join(" · ", parts);
		return status.LastError is { } error
			? $"{text}\nLast error{(status.LastErrorAt is { } at ? $" ({Time(at)})" : "")}: {error}"
			: text;
	}

	/// <summary>The Day trade widget's note on where today's trades come from; null when Flex isn't set up.</summary>
	public static string? DescribeTodayTrades(FlexImportStatusDto status) =>
		!status.IsConfigured ? null
		: !status.HasTradeConfirmQuery
			? "Trades come from IB Flex through yesterday; add a Trade Confirmation query in Settings → Account history for today's."
		: status.LastTradeConfirmImport is { } at
			? $"Today's trades via IB Flex, up to ~{(int)FlexConstants.TradeConfirmInterval.TotalMinutes} min delayed (last import {Time(at)})."
		: "Today's trades via IB Flex, not imported yet.";

	private static string Date(DateOnly date) =>
		date.ToString("d MMM yyyy", CultureInfo.CurrentCulture);

	private static string Time(DateTimeOffset time)
	{
		var local = time.ToLocalTime();
		return local.Date == DateTime.Today
			? local.ToString("HH:mm", CultureInfo.CurrentCulture)
			: local.ToString("d MMM HH:mm", CultureInfo.CurrentCulture);
	}
}
