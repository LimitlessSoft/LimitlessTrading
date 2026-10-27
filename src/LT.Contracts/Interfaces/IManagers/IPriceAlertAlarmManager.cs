using LT.Contracts.Dtos.PriceAlerts;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// The price alerts that fired and the user hasn't dismissed yet: the main window shows them in a
/// popup, and the alarm sound plays at once and again every
/// <c>PriceAlertConstants.AlarmRepeatInterval</c> until every one is dismissed. Fed with
/// <see cref="IPriceAlertManager.AlertTriggered"/> (wired by PriceAlertService).
/// </summary>
public interface IPriceAlertAlarmManager
{
	/// <summary>An alert was added or dismissed; raised on the changing thread.</summary>
	event Action? Changed;

	/// <summary>True while an alert waits to be dismissed, i.e. while the alarm sounds.</summary>
	bool IsRinging { get; }

	/// <summary>The alerts waiting to be dismissed, in the order they fired.</summary>
	IReadOnlyList<PriceAlertDto> GetPending();

	/// <summary>
	/// An alert fired: it waits to be dismissed (one that fired again replaces its earlier entry)
	/// and the alarm starts unless it sounds already. Any thread; never throws.
	/// </summary>
	void OnTriggered(PriceAlertDto alert);

	/// <summary>Dismisses one alert; the alarm stops with the last one. Nothing when it is gone.</summary>
	void Dismiss(string id);

	/// <summary>Dismisses every alert and stops the alarm.</summary>
	void DismissAll();
}
