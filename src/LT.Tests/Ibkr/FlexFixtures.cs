namespace LT.Tests.Ibkr;

/// <summary>Hand-written Flex statements: fake accounts (U0000001…), fake amounts, IB's layout.</summary>
public static class FlexFixtures
{
	public const string Activity = """
		<FlexQueryResponse queryName="LT activity" type="AF">
		<FlexStatements count="2">
		<FlexStatement accountId="U0000001" fromDate="20261019" toDate="20261023" period="" whenGenerated="20261024;030000">
		<AccountInformation accountId="U0000001" currency="USD" dateOpened="20240102" />
		<EquitySummaryInBase>
		<EquitySummaryByReportDateInBase accountId="U0000001" currency="USD" reportDate="20261021" cash="500" stock="9500" total="10000" />
		<EquitySummaryByReportDateInBase accountId="U0000001" currency="USD" reportDate="20261022" cash="1500" stock="9600" total="11100" />
		<EquitySummaryByReportDateInBase accountId="U0000001" currency="USD" reportDate="20261023" cash="1500" stock="9550" total="11050" />
		</EquitySummaryInBase>
		<CashTransactions>
		<CashTransaction accountId="U0000001" currency="EUR" fxRateToBase="1.1" type="Deposits/Withdrawals" description="CASH RECEIPTS" dateTime="20261022" reportDate="20261022" amount="909.090909" levelOfDetail="DETAIL" />
		<CashTransaction accountId="U0000001" currency="BASE_SUMMARY" type="Deposits/Withdrawals" reportDate="20261022" amount="1000" levelOfDetail="SUMMARY" />
		<CashTransaction accountId="U0000001" currency="USD" type="Dividends" reportDate="20261023" amount="12.5" levelOfDetail="DETAIL" />
		</CashTransactions>
		<Trades>
		<Trade accountId="U0000001" currency="USD" assetCategory="STK" symbol="FAKE" description="FAKE CORP" conid="1001" multiplier="1" tradeID="5001" ibExecID="0000a1b2.0001.01.01" buySell="BUY" quantity="100" tradePrice="10.5" ibCommission="-1.25" ibCommissionCurrency="USD" dateTime="20261022;093512" tradeDate="20261022" fifoPnlRealized="0" exchange="ISLAND" levelOfDetail="EXECUTION" />
		<Trade accountId="U0000001" currency="USD" assetCategory="STK" symbol="FAKE" conid="1001" multiplier="1" tradeID="5002" ibExecID="0000a1b2.0002.01.01" buySell="SELL" quantity="-100" tradePrice="11" ibCommission="-1.30" ibCommissionCurrency="USD" dateTime="20261022;154501" tradeDate="20261022" fifoPnlRealized="47.45" levelOfDetail="EXECUTION" />
		<Lot accountId="U0000001" symbol="FAKE" conid="1001" quantity="100" levelOfDetail="CLOSED_LOT" />
		<Trade accountId="U0000001" symbol="FAKE" conid="1001" buySell="SELL" quantity="-100" tradePrice="11" levelOfDetail="ORDER" />
		<Trade accountId="U0000001" currency="USD" assetCategory="STK" symbol="FAKE" conid="1001" tradeID="5003" ibExecID="0000a1b2.0003.01.01" buySell="BUY" quantity="10" tradePrice="10" dateTime="20261023;100000" levelOfDetail="EXECUTION" />
		<Trade accountId="U0000001" currency="USD" assetCategory="STK" symbol="FAKE" conid="1001" tradeID="5004" origTradeID="5003" buySell="BUY (Ca.)" quantity="-10" tradePrice="10" dateTime="20261023;100500" levelOfDetail="EXECUTION" />
		</Trades>
		</FlexStatement>
		<FlexStatement accountId="U0000002" fromDate="20261019" toDate="20261023">
		<EquitySummaryInBase>
		<EquitySummaryByReportDateInBase reportDate="20261023" total="2,500.75" currency="CHF" />
		<EquitySummaryByReportDateInBase reportDate="" total="1" />
		</EquitySummaryInBase>
		<Trades>
		<Trade assetCategory="OPT" symbol="FAKE  261120C00012000" underlyingSymbol="FAKE" conid="2002" multiplier="100" tradeID="6001" buySell="SELL" quantity="-2" tradePrice="1.2" dateTime="20261023;110000" />
		<Trade assetCategory="STK" symbol="BAD" conid="3003" tradeID="6002" buySell="BUY" quantity="" tradePrice="1" />
		</Trades>
		</FlexStatement>
		</FlexStatements>
		</FlexQueryResponse>
		""";

	public const string TradeConfirm = """
		<FlexQueryResponse queryName="LT today" type="TCF">
		<FlexStatements count="1">
		<FlexStatement accountId="U0000001" fromDate="20261026" toDate="20261026">
		<TradeConfirms>
		<TradeConfirm accountId="U0000001" currency="USD" assetCategory="STK" symbol="FAKE" conid="1001" tradeID="7001" execID="0000a1b2.0009.01.01" buySell="BUY" quantity="50" price="12" commission="-0.5" commissionCurrency="USD" dateTime="2026-10-26, 09:31:00" tradeDate="2026-10-26" levelOfDetail="EXECUTION" />
		<TradeConfirm accountId="U0000001" assetCategory="STK" symbol="FAKE" conid="1001" buySell="SELL" quantity="-50" price="12" levelOfDetail="ORDER" />
		</TradeConfirms>
		</FlexStatement>
		</FlexStatements>
		</FlexQueryResponse>
		""";

	public const string Empty = """
		<FlexQueryResponse queryName="LT activity" type="AF">
		<FlexStatements count="1">
		<FlexStatement accountId="U0000001" fromDate="20221019" toDate="20231018">
		<EquitySummaryInBase />
		<CashTransactions />
		<Trades />
		</FlexStatement>
		</FlexStatements>
		</FlexQueryResponse>
		""";

	public static string SendSuccess(string reference = "1234567890") =>
		$"""
			<FlexStatementResponse timestamp="26 October, 2026 09:51 AM EDT">
			<Status>Success</Status>
			<ReferenceCode>{reference}</ReferenceCode>
			<url>https://ndcdyn.interactivebrokers.com/AccountManagement/FlexWebService/GetStatement</url>
			</FlexStatementResponse>
			""";

	public static string Fail(int code, string message) =>
		$"""
			<FlexStatementResponse timestamp="26 October, 2026 09:51 AM EDT">
			<Status>Fail</Status>
			<ErrorCode>{code}</ErrorCode>
			<ErrorMessage>{message}</ErrorMessage>
			</FlexStatementResponse>
			""";
}
