using LT.Contracts.Dtos.Executions;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// Plays the fill sound (setting <c>PlayFillSound</c>) when an execution IB returns wasn't seen
/// before. Fed with every executions answer of the trade journal
/// (<see cref="ITradeJournalManager.ExecutionsReceived"/>), so fills are noticed at most
/// <c>AccountConstants.ExecutionsRefreshInterval</c> late.
/// </summary>
public interface IFillAlertManager
{
	/// <summary>
	/// The connection started or ended: the next executions are IB's replay of the day's fills,
	/// remembered without a sound.
	/// </summary>
	void BeginSession();

	/// <summary>
	/// Remembers the executions (by ExecId) and plays the sound once if any is new, unless they are
	/// the session's replay. Returns how many were new; never throws.
	/// </summary>
	int OnExecutions(IReadOnlyList<ExecutionDto> executions);
}
