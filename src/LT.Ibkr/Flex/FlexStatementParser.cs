using System.Xml;
using System.Xml.Linq;
using LT.Contracts.Dtos.Flex;
using LT.Contracts.Helpers;

namespace LT.Ibkr.Flex;

/// <summary>
/// Reads a FlexQueryResponse (Activity or Trade Confirmation, XML) into DTOs. Tolerant: missing
/// optional attributes stay empty / null, unknown sections are ignored, several accounts (one
/// FlexStatement each) are read in one go. What the app reads, per section:
/// <list type="bullet">
/// <item>EquitySummaryInBase / EquitySummaryByReportDateInBase: accountId, reportDate, total, currency.</item>
/// <item>CashTransactions / CashTransaction of type "Deposits/Withdrawals" (detail rows): accountId,
/// amount × fxRateToBase, reportDate (else dateTime, settleDate), description.</item>
/// <item>Trades / Trade (execution rows) and TradeConfirms / TradeConfirm: accountId, tradeID,
/// ibExecID / execID, conid, symbol, underlyingSymbol, description, assetCategory, multiplier,
/// buySell, quantity, tradePrice / price, ibCommission / commission (+ currency), currency,
/// exchange, dateTime, tradeDate, fifoPnlRealized; "(Ca.)" rows void their origTradeID.</item>
/// <item>AccountInformation: accountId, dateOpened (optional; ends the back-fill early).</item>
/// </list>
/// </summary>
public static class FlexStatementParser
{
	private const string ExecutionLevel = "EXECUTION";

	public static FlexStatementDto Parse(XDocument document, TimeZoneInfo zone)
	{
		var root = document.Root!;
		var accounts = new List<string>();
		var nav = new List<FlexNavDto>();
		var cashFlows = new List<FlexCashFlowDto>();
		var trades = new List<FlexTradeDto>();
		var cancelled = new List<string>();
		var opened = new Dictionary<string, DateOnly>();
		var hasNav = false;
		var hasTrades = false;

		foreach (var statement in root.Descendants("FlexStatement"))
		{
			var statementAccount = Attr(statement, "accountId");
			if (statementAccount.Length > 0 && !accounts.Contains(statementAccount))
				accounts.Add(statementAccount);

			hasNav |= statement.Descendants("EquitySummaryInBase").Any();
			foreach (var row in statement.Descendants("EquitySummaryByReportDateInBase"))
				if (ParseNav(row, statementAccount) is { } value)
					nav.Add(value);

			foreach (var row in statement.Descendants("CashTransaction"))
				if (ParseCashFlow(row, statementAccount, zone) is { } flow)
					cashFlows.Add(flow);

			hasTrades |=
				statement.Descendants("Trades").Any()
				|| statement.Descendants("TradeConfirms").Any();
			var rows = statement.Descendants("Trade").Concat(statement.Descendants("TradeConfirm"));
			foreach (var row in rows)
			{
				var level = Attr(row, "levelOfDetail");
				if (
					level.Length > 0
					&& !level.Equals(ExecutionLevel, StringComparison.OrdinalIgnoreCase)
				)
					continue;
				// "BUY (Ca.)" / "SELL (Ca.)": a busted trade; it voids the original
				if (Attr(row, "buySell").Contains("Ca.", StringComparison.OrdinalIgnoreCase))
				{
					var original = Attr(row, "origTradeID");
					if (original.Length > 0)
						cancelled.Add(original);
					continue;
				}
				if (ParseTrade(row, statementAccount, zone) is { } trade)
					trades.Add(trade);
			}

			foreach (var info in statement.Descendants("AccountInformation"))
			{
				var account = AttrOr(info, "accountId", statementAccount);
				if (
					account.Length > 0
					&& FlexHelpers.ParseDate(Attr(info, "dateOpened")) is { } date
				)
					opened[account] = date;
			}
		}

		foreach (var account in nav.Select(x => x.Account).Concat(trades.Select(x => x.Account)))
			if (account.Length > 0 && !accounts.Contains(account))
				accounts.Add(account);

		return new FlexStatementDto
		{
			QueryType = Attr(root, "type"),
			Accounts = accounts,
			Nav = nav,
			CashFlows = cashFlows,
			Trades = trades.Where(x => !cancelled.Contains(x.TradeId)).ToList(),
			CancelledTradeIds = cancelled,
			AccountsOpened = opened,
			HasNavSection = hasNav,
			HasTradesSection = hasTrades,
		};
	}

	/// <summary>Parses <paramref name="xml"/>; null when it isn't XML or isn't a FlexQueryResponse.</summary>
	public static XDocument? TryLoad(string xml)
	{
		try
		{
			return XDocument.Parse(xml);
		}
		catch (XmlException)
		{
			return null;
		}
	}

	private static FlexNavDto? ParseNav(XElement row, string statementAccount)
	{
		var account = AttrOr(row, "accountId", statementAccount);
		if (
			FlexHelpers.ParseDate(Attr(row, "reportDate")) is not { } date
			|| FlexHelpers.ParseDecimal(Attr(row, "total")) is not { } total
		)
			return null;
		return new FlexNavDto
		{
			Account = account,
			Date = date,
			Total = total,
			Currency = Attr(row, "currency"),
		};
	}

	private static FlexCashFlowDto? ParseCashFlow(
		XElement row,
		string statementAccount,
		TimeZoneInfo zone
	)
	{
		var type = Attr(row, "type");
		if (
			!type.Contains("Deposit", StringComparison.OrdinalIgnoreCase)
			&& !type.Contains("Withdrawal", StringComparison.OrdinalIgnoreCase)
		)
			return null;
		// Summary rows repeat the detail rows (currency BASE_SUMMARY)
		var level = Attr(row, "levelOfDetail");
		if (
			level.Equals("SUMMARY", StringComparison.OrdinalIgnoreCase)
			|| Attr(row, "currency").Equals("BASE_SUMMARY", StringComparison.OrdinalIgnoreCase)
		)
			return null;
		if (FlexHelpers.ParseDouble(Attr(row, "amount")) is not { } amount)
			return null;
		var date =
			FlexHelpers.ParseDate(Attr(row, "reportDate"))
			?? FlexHelpers.ParseDateTime(Attr(row, "dateTime"), zone).Date
			?? FlexHelpers.ParseDate(Attr(row, "settleDate"));
		if (date is not { } day)
			return null;
		return new FlexCashFlowDto
		{
			Account = AttrOr(row, "accountId", statementAccount),
			Date = day,
			Amount = amount * (FlexHelpers.ParseDouble(Attr(row, "fxRateToBase")) ?? 1),
			Description = Attr(row, "description"),
		};
	}

	private static FlexTradeDto? ParseTrade(
		XElement row,
		string statementAccount,
		TimeZoneInfo zone
	)
	{
		var side = Attr(row, "buySell").ToUpperInvariant();
		var quantity = FlexHelpers.ParseDecimal(Attr(row, "quantity"));
		var price = FlexHelpers.ParseDouble(AttrOr(row, "tradePrice", Attr(row, "price")));
		if (quantity is not { } signed || signed == 0 || price is not { } tradePrice)
			return null;
		if (side != "BUY" && side != "SELL")
			side = signed > 0 ? "BUY" : "SELL";

		var (date, time) = FlexHelpers.ParseDateTime(Attr(row, "dateTime"), zone);
		var commission = FlexHelpers.ParseDouble(
			AttrOr(row, "ibCommission", Attr(row, "commission"))
		);
		return new FlexTradeDto
		{
			Account = AttrOr(row, "accountId", statementAccount),
			TradeId = Attr(row, "tradeID"),
			ExecId = AttrOr(row, "ibExecID", Attr(row, "execID")),
			ConId = int.TryParse(Attr(row, "conid"), out var conId) ? conId : 0,
			Symbol = Attr(row, "symbol"),
			UnderlyingSymbol = Attr(row, "underlyingSymbol"),
			Description = Attr(row, "description"),
			AssetCategory = Attr(row, "assetCategory"),
			Multiplier = FlexHelpers.ParseDouble(Attr(row, "multiplier")) is > 0 and var m
				? m
				: null,
			Side = side,
			Quantity = Math.Abs(signed),
			Price = tradePrice,
			// IB reports what the fill cost as a negative amount
			Commission = commission is { } c ? Math.Abs(c) : null,
			CommissionCurrency = AttrOr(
				row,
				"ibCommissionCurrency",
				Attr(row, "commissionCurrency")
			),
			Currency = Attr(row, "currency"),
			Exchange = Attr(row, "exchange"),
			Time = time,
			TradeDate = FlexHelpers.ParseDate(Attr(row, "tradeDate")) ?? date,
			RealizedPnl = FlexHelpers.ParseDouble(Attr(row, "fifoPnlRealized")),
		};
	}

	private static string Attr(XElement element, string name) =>
		element.Attribute(name)?.Value.Trim() ?? string.Empty;

	private static string AttrOr(XElement element, string name, string fallback)
	{
		var value = Attr(element, name);
		return value.Length > 0 ? value : fallback;
	}
}
