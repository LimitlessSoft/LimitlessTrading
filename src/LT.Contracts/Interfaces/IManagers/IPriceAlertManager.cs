using LT.Contracts.Dtos.PriceAlerts;
using LT.Contracts.Requests.PriceAlerts;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// Price alerts: a level on a contract that fires once when the price crosses it, in the direction
/// told from the price when it was set (or last moved). Saved in price-alerts.json. While the app
/// is connected <see cref="WatchAsync"/> follows every contract with an active alert on a quote
/// line of its own (through <see cref="IMarketDataManager"/>), whether a chart shows it or not.
/// </summary>
public interface IPriceAlertManager
{
	/// <summary>An alert was added, moved, removed or fired; raised on the changing thread.</summary>
	event Action? AlertsChanged;

	/// <summary>An alert fired (once per alert); raised on the price stream's thread.</summary>
	event Action<PriceAlertDto>? AlertTriggered;

	/// <summary>Every alert, oldest first.</summary>
	IReadOnlyList<PriceAlertDto> GetAlerts();

	/// <summary>The alerts of one contract (by ConId), active and triggered, oldest first.</summary>
	IReadOnlyList<PriceAlertDto> GetAlerts(int conId);

	/// <summary>
	/// Adds an active alert; its direction is told from <see cref="CreatePriceAlertRequest.CurrentPrice"/>.
	/// Refused above <c>PriceAlertConstants.MaxActiveAlerts</c> or when it would watch more than
	/// <c>PriceAlertConstants.MaxWatchedContracts</c> contracts.
	/// </summary>
	PriceAlertDto Create(CreatePriceAlertRequest request);

	/// <summary>Moves an alert to a new level and arms it again (a triggered one too).</summary>
	PriceAlertDto Move(MovePriceAlertRequest request);

	/// <summary>Removes the alert; nothing when it is gone already.</summary>
	void Delete(string id);

	/// <summary>Removes the triggered alerts of one contract; returns how many.</summary>
	int DeleteTriggered(int conId);

	/// <summary>
	/// A price of the contract: fires (once) every active alert of it the price has crossed, saves
	/// them and raises <see cref="AlertTriggered"/>. Returns the alerts it fired; never throws.
	/// </summary>
	IReadOnlyList<PriceAlertDto> OnPrice(int conId, double price);

	/// <summary>
	/// Follows the price of every contract with an active alert until cancelled (the connection's
	/// session): one quote line per contract, opened and closed as alerts come and go. A stream
	/// that fails (no line left, IB refused it) is logged and tried again after
	/// <c>PriceAlertConstants.RetryDelay</c>.
	/// </summary>
	Task WatchAsync(CancellationToken cancellationToken);
}
