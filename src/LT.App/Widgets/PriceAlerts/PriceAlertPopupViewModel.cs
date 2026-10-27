using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.PriceAlerts;

/// <summary>
/// The fired price alerts waiting to be dismissed (<see cref="IPriceAlertAlarmManager"/>), newest
/// first. Dismissing the last one stops the alarm; the main window then closes the popup.
/// </summary>
public partial class PriceAlertPopupViewModel : ViewModelBase, IDisposable
{
	private readonly IPriceAlertAlarmManager _alarmManager;
	private readonly DisplayTimeStore _displayTime;

	public PriceAlertPopupViewModel(
		IPriceAlertAlarmManager alarmManager,
		DisplayTimeStore displayTime
	)
	{
		_alarmManager = alarmManager;
		_displayTime = displayTime;
		Refresh();
		alarmManager.Changed += OnChanged;
	}

	public ObservableCollection<PriceAlertPopupItem> Alerts { get; } = [];

	/// <summary>"Price alert" for one, "3 price alerts" for more.</summary>
	[ObservableProperty]
	public partial string Title { get; set; } = "Price alert";

	[ObservableProperty]
	public partial bool HasSeveral { get; set; }

	[RelayCommand]
	private void Dismiss(PriceAlertPopupItem item) => _alarmManager.Dismiss(item.Id);

	[RelayCommand]
	private void DismissAll() => _alarmManager.DismissAll();

	public void Dispose()
	{
		_alarmManager.Changed -= OnChanged;
		GC.SuppressFinalize(this);
	}

	// From a price stream's thread (a new alert) or the UI thread (a dismissal)
	private void OnChanged() => Dispatcher.UIThread.Post(Refresh);

	private void Refresh()
	{
		var pending = _alarmManager.GetPending();
		Alerts.Clear();
		for (var i = pending.Count - 1; i >= 0; i--)
		{
			var alert = pending[i];
			Alerts.Add(
				new PriceAlertPopupItem(
					alert.Id,
					ContractHelpers.GetDisplayName(alert.Contract),
					PriceAlertHelpers.GetDescription(alert.Condition, alert.Price),
					alert.TriggerPrice is { } price
						? $"Last {PriceAlertHelpers.FormatPrice(price)}"
						: null,
					string.IsNullOrWhiteSpace(alert.Note) ? null : alert.Note.Trim(),
					alert.TriggeredAt is { } time
						? _displayTime.FormatDayTime(time, "HH:mm:ss")
						: ""
				)
			);
		}
		HasSeveral = pending.Count > 1;
		Title = pending.Count > 1 ? $"{pending.Count} price alerts" : "Price alert";
	}
}

/// <summary>One fired alert in the popup.</summary>
public record PriceAlertPopupItem(
	string Id,
	string Symbol,
	string Description,
	string? LastText,
	string? Note,
	string TimeText
);
