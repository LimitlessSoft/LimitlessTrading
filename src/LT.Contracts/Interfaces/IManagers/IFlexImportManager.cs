using LT.Contracts.Dtos.Flex;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// Imports account history and trades from IB's Flex Web Service into the account history and
/// the trade journal: a back-fill to the account's start on first configuration, the Activity
/// statement once a day, the Trade Confirmation statement every
/// <c>FlexConstants.TradeConfirmInterval</c>, and on demand. Imports never run in parallel.
/// </summary>
public interface IFlexImportManager
{
	/// <summary>Raised on any thread when the status changed (import started / finished / failed).</summary>
	event Action? StatusChanged;

	FlexImportStatusDto GetStatus();

	/// <summary>
	/// Runs due imports until <paramref name="stoppingToken"/> is cancelled, waking for the next
	/// due one, a settings change or <see cref="RequestImport"/>. Run once, by the app's host.
	/// </summary>
	Task RunAsync(CancellationToken stoppingToken);

	/// <summary>
	/// Runs what is due now and the Trade Confirmation statement even if it isn't (F5, Import
	/// now); waits for an import already running first. Errors end up in the status, not thrown.
	/// </summary>
	Task ImportNowAsync(CancellationToken cancellationToken = default);
}
