using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.Flex;
using LT.Contracts.Dtos.Performance;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// Records every fill seen (IB only returns the current day's) and each account's intraday P&amp;L
/// curve in the local trade journal, and serves trades and statistics per trading day (New York
/// date, <c>TradeJournalHelpers.GetTradeDay</c>).
/// </summary>
public interface ITradeJournalManager
{
	/// <summary>Raised on any thread after new or updated fills were recorded (or the error changed).</summary>
	event Action? FillsChanged;

	/// <summary>Raised on any thread after a P&amp;L update was recorded.</summary>
	event Action? PnlChanged;

	/// <summary>
	/// Raised on the requesting thread with every execution of each successful executions answer
	/// (<see cref="RecordExecutionsAsync"/>), recorded before or not. Handlers must not throw.
	/// </summary>
	event Action<IReadOnlyList<ExecutionDto>>? ExecutionsReceived;

	/// <summary>Why the last executions request failed (e.g. an IB error); null when it worked.</summary>
	string? ExecutionsError { get; }

	/// <summary>Today's trading day.</summary>
	DateOnly Today { get; }

	/// <summary>
	/// Records while connected, until <paramref name="cancellationToken"/> is cancelled (the
	/// connection ended): the P&amp;L of each managed account (up to
	/// <c>TradeJournalConstants.MaxPnlAccounts</c>, the reqPnL subscription shared with the Account
	/// widget) and the day's executions now and every <c>AccountConstants.ExecutionsRefreshInterval</c>.
	/// Saves pending samples when it ends.
	/// </summary>
	Task RecordAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Makes a running <see cref="RecordAsync"/> ask for executions
	/// <c>TradeJournalConstants.FillRefreshDelay</c> from now rather than at its next poll (an
	/// order reported a fill). Requests coalesce: any number made before that request starts, or
	/// while it runs, add one more request at most. Nothing happens while not recording.
	/// </summary>
	void RequestExecutionsRefresh();

	/// <summary>Asks IB for the day's executions once and merges them into the journal; returns the fills added or updated.</summary>
	Task<int> RecordExecutionsAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Merges Flex executions into the journal (deduplicated by IB execution id, else tradeID, so a
	/// Flex fill and the same socket fill stay one) and drops fills whose tradeID is in
	/// <paramref name="cancelledTradeIds"/>. Returns the fills added, updated or removed.
	/// </summary>
	int ImportFlexTrades(
		IReadOnlyList<FlexTradeDto> trades,
		IReadOnlyCollection<string> cancelledTradeIds
	);

	/// <summary>Adds an account's daily P&amp;L to today's curve (no value: ignored).</summary>
	void RecordPnl(string account, AccountPnlDto pnl);

	/// <summary>Writes curve points not saved yet.</summary>
	void Flush();

	/// <summary>Days with recorded fills or P&amp;L, and today, oldest first.</summary>
	IReadOnlyList<DateOnly> GetDays();

	IReadOnlyList<PnlSampleDto> GetPnlCurve(DateOnly day, string account);

	/// <summary>
	/// Trades closed on <paramref name="day"/> by <paramref name="account"/> (null: every
	/// account) and their statistics. Fills of earlier recorded days count too, so a position
	/// carried overnight closes against its recorded entry.
	/// </summary>
	DayPerformanceDto GetDayPerformance(DateOnly day, string? account);

	/// <summary>
	/// Every recorded fill of the contract <paramref name="contractId"/> by
	/// <paramref name="account"/> (null: every account), over all recorded days, and the entry →
	/// exit pairs of its closed quantity (FIFO, <c>TradeMatchingHelpers.MatchLegs</c>).
	/// </summary>
	ContractTradesDto GetContractTrades(int contractId, string? account);
}
