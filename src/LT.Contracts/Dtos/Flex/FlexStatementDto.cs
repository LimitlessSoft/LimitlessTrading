namespace LT.Contracts.Dtos.Flex;

/// <summary>What the app reads from a Flex statement (Activity or Trade Confirmation), every account in it.</summary>
public class FlexStatementDto
{
	/// <summary>IB's query type: "AF" Activity, "TCF" Trade Confirmation; empty when not sent.</summary>
	public string QueryType { get; set; } = string.Empty;
	public IReadOnlyList<string> Accounts { get; set; } = [];
	public IReadOnlyList<FlexNavDto> Nav { get; set; } = [];
	public IReadOnlyList<FlexCashFlowDto> CashFlows { get; set; } = [];
	public IReadOnlyList<FlexTradeDto> Trades { get; set; } = [];

	/// <summary>tradeIDs that a cancellation row ("BUY (Ca.)") in the statement voids.</summary>
	public IReadOnlyList<string> CancelledTradeIds { get; set; } = [];

	/// <summary>Account opening dates, when the query has Account Information with dateOpened.</summary>
	public IReadOnlyDictionary<string, DateOnly> AccountsOpened { get; set; } =
		new Dictionary<string, DateOnly>();

	/// <summary>The statement had a Net Asset Value section (even if empty).</summary>
	public bool HasNavSection { get; set; }

	/// <summary>The statement had a Trades or Trade Confirmations section (even if empty).</summary>
	public bool HasTradesSection { get; set; }

	public bool IsEmpty => Nav.Count == 0 && CashFlows.Count == 0 && Trades.Count == 0;
}
