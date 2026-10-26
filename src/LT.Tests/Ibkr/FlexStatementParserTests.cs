using System.Xml.Linq;
using LT.Contracts.Dtos.Flex;
using LT.Contracts.Helpers;
using LT.Ibkr.Flex;

namespace LT.Tests.Ibkr;

public class FlexStatementParserTests
{
	private static readonly TimeZoneInfo NewYork = FlexHelpers.GetStatementTimeZone();

	private static FlexStatementDto Parse(string xml) =>
		FlexStatementParser.Parse(XDocument.Parse(xml), NewYork);

	[Fact]
	public void Reads_daily_nav_of_every_account_and_skips_rows_without_a_date()
	{
		var statement = Parse(FlexFixtures.Activity);

		Assert.Equal("AF", statement.QueryType);
		Assert.Equal(["U0000001", "U0000002"], statement.Accounts);
		Assert.True(statement.HasNavSection);
		Assert.Equal(4, statement.Nav.Count);
		var first = statement.Nav[0];
		Assert.Equal(
			("U0000001", new DateOnly(2026, 10, 21), 10000m, "USD"),
			(first.Account, first.Date, first.Total, first.Currency)
		);
		// accountId missing on the row: the statement's; thousands separator accepted
		var other = statement.Nav[3];
		Assert.Equal(("U0000002", 2500.75m, "CHF"), (other.Account, other.Total, other.Currency));
	}

	[Fact]
	public void Reads_deposits_in_base_currency_once_and_ignores_other_cash_transactions()
	{
		var flow = Assert.Single(Parse(FlexFixtures.Activity).CashFlows);

		Assert.Equal("U0000001", flow.Account);
		Assert.Equal(new DateOnly(2026, 10, 22), flow.Date);
		Assert.Equal(1000, flow.Amount, 3);
	}

	[Fact]
	public void Reads_execution_rows_with_commission_as_a_cost_and_new_york_time()
	{
		var statement = Parse(FlexFixtures.Activity);

		var buy = statement.Trades.Single(x => x.TradeId == "5001");
		Assert.Equal("0000a1b2.0001.01.01", buy.ExecId);
		Assert.Equal(1001, buy.ConId);
		Assert.Equal("BUY", buy.Side);
		Assert.Equal(100m, buy.Quantity);
		Assert.Equal(10.5, buy.Price);
		Assert.Equal(1.25, buy.Commission);
		Assert.Equal("USD", buy.CommissionCurrency);
		Assert.Equal(1, buy.Multiplier);
		Assert.Equal("ISLAND", buy.Exchange);
		// 09:35:12 EDT
		Assert.Equal(new DateTimeOffset(2026, 10, 22, 13, 35, 12, TimeSpan.Zero), buy.Time);

		var sell = statement.Trades.Single(x => x.TradeId == "5002");
		Assert.Equal(("SELL", 100m, 47.45), (sell.Side, sell.Quantity, sell.RealizedPnl));
	}

	[Fact]
	public void Skips_order_and_lot_rows_and_drops_cancelled_trades()
	{
		var statement = Parse(FlexFixtures.Activity);

		Assert.Equal(["5001", "5002", "6001"], statement.Trades.Select(x => x.TradeId));
		Assert.Equal(["5003"], statement.CancelledTradeIds);
	}

	[Fact]
	public void Tolerates_missing_optional_attributes()
	{
		var option = Parse(FlexFixtures.Activity).Trades.Single(x => x.TradeId == "6001");

		Assert.Equal("U0000002", option.Account);
		Assert.Equal(("FAKE", "FAKE  261120C00012000"), (option.UnderlyingSymbol, option.Symbol));
		Assert.Equal(100, option.Multiplier);
		Assert.Null(option.Commission);
		Assert.Null(option.RealizedPnl);
		Assert.Equal(string.Empty, option.ExecId);
		Assert.Equal(new DateOnly(2026, 10, 23), option.TradeDate);
	}

	[Fact]
	public void Reads_account_opening_dates()
	{
		var statement = Parse(FlexFixtures.Activity);

		Assert.Equal(new DateOnly(2024, 1, 2), statement.AccountsOpened["U0000001"]);
		Assert.False(statement.AccountsOpened.ContainsKey("U0000002"));
	}

	[Fact]
	public void Reads_trade_confirmations_with_their_own_attribute_names()
	{
		var statement = Parse(FlexFixtures.TradeConfirm);

		Assert.Equal("TCF", statement.QueryType);
		Assert.True(statement.HasTradesSection);
		Assert.False(statement.HasNavSection);
		var fill = Assert.Single(statement.Trades);
		Assert.Equal("0000a1b2.0009.01.01", fill.ExecId);
		Assert.Equal(12, fill.Price);
		Assert.Equal(0.5, fill.Commission);
		Assert.Equal(new DateTimeOffset(2026, 10, 26, 13, 31, 0, TimeSpan.Zero), fill.Time);
	}

	[Fact]
	public void An_empty_statement_is_empty_but_has_its_sections()
	{
		var statement = Parse(FlexFixtures.Empty);

		Assert.True(statement.IsEmpty);
		Assert.True(statement.HasNavSection);
		Assert.True(statement.HasTradesSection);
	}
}
