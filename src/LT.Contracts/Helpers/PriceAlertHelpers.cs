using System.Globalization;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.PriceAlerts;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;

namespace LT.Contracts.Helpers;

public static class PriceAlertHelpers
{
	/// <summary>
	/// The direction of an alert at <paramref name="level"/> set while the price is
	/// <paramref name="currentPrice"/>: up when the level is above it, down otherwise (a level at
	/// the price fires on the next price at or below it).
	/// </summary>
	public static PriceAlertCondition GetCondition(double level, double currentPrice) =>
		level > currentPrice ? PriceAlertCondition.CrossingUp : PriceAlertCondition.CrossingDown;

	/// <summary>
	/// Whether <paramref name="price"/> is past the level in the alert's direction. A level, not a
	/// pair of prices: a price that gapped through it (or moved while disconnected) fires on the
	/// first price seen.
	/// </summary>
	public static bool IsCrossed(PriceAlertCondition condition, double level, double price) =>
		condition switch
		{
			PriceAlertCondition.CrossingUp => price >= level,
			PriceAlertCondition.CrossingDown => price <= level,
			_ => false,
		};

	/// <summary>
	/// The price alerts compare: the last trade, or the bid / ask midpoint for forex (no trades),
	/// like the chart's live prices. Delayed quotes count (all an account without market data
	/// subscriptions gets). Null when there is none yet.
	/// </summary>
	public static double? GetPrice(QuoteDto quote, string securityType)
	{
		if (securityType == MarketDataConstants.ForexType)
			return quote is { Bid: > 0 and var bid, Ask: > 0 and var ask } ? (bid + ask) / 2 : null;
		return quote.Last is > 0 and var last ? last : null;
	}

	/// <summary>ConIds of the contracts with an active alert.</summary>
	public static HashSet<int> GetWatchedConIds(IEnumerable<PriceAlertEntity> alerts) =>
		[.. alerts.Where(x => x.TriggeredAt == null).Select(x => x.Contract.ConId)];

	/// <summary>
	/// Why one more active alert on contract <paramref name="conId"/> can't be added; null when it
	/// can. <paramref name="alerts"/> are the stored ones, without the one being armed.
	/// </summary>
	public static string? GetLimitError(IReadOnlyCollection<PriceAlertEntity> alerts, int conId)
	{
		if (alerts.Count(x => x.TriggeredAt == null) >= PriceAlertConstants.MaxActiveAlerts)
			return PriceAlertValidationCodes.PAVC_005.GetValidationMessage(
				PriceAlertConstants.MaxActiveAlerts
			);
		var watched = GetWatchedConIds(alerts);
		if (!watched.Contains(conId) && watched.Count >= PriceAlertConstants.MaxWatchedContracts)
			return PriceAlertValidationCodes.PAVC_006.GetValidationMessage(
				PriceAlertConstants.MaxWatchedContracts
			);
		return null;
	}

	/// <summary>A level as written in labels and notifications: at least 2 decimals, up to 6.</summary>
	public static string FormatPrice(double price) =>
		price.ToString("#,0.00####", CultureInfo.CurrentCulture);

	/// <summary>"crossed above 190.00" / "crossed below 185.50".</summary>
	public static string GetDescription(PriceAlertCondition condition, double level) =>
		$"{(condition == PriceAlertCondition.CrossingUp ? "crossed above" : "crossed below")} {FormatPrice(level)}";

	/// <summary>The notification of a fired alert: "AAPL crossed above 190.00 (last 190.02)" and its note.</summary>
	public static string GetTriggeredText(PriceAlertDto alert)
	{
		var text =
			$"{ContractHelpers.GetDisplayName(alert.Contract)} {GetDescription(alert.Condition, alert.Price)}";
		if (alert.TriggerPrice is { } price)
			text += $" (last {FormatPrice(price)})";
		if (!string.IsNullOrWhiteSpace(alert.Note))
			text += $" — {alert.Note.Trim()}";
		return text;
	}
}
