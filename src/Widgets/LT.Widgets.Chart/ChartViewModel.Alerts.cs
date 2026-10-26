using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LSCore.Exceptions;
using LT.Contracts.Dtos.PriceAlerts;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.PriceAlerts;

namespace LT.App.Widgets.Chart;

/// <summary>
/// Price alerts of the charted contract (<see cref="IPriceAlertManager"/>): drawn as lines on the
/// chart, added at a price (right-click → Add alert, Alt+A), moved by dragging the line (which
/// arms a triggered one again) and removed with its ✕ or from the menu. The direction is told from
/// the chart's last price. They fire whether or not a chart shows them.
/// </summary>
public partial class ChartViewModel
{
	private readonly IPriceAlertManager _priceAlertManager;
	private int _alertsQueued;

	/// <summary>The charted contract's alerts, active and triggered.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasTriggeredAlerts))]
	public partial IReadOnlyList<PriceAlertDto> Alerts { get; set; } = [];

	/// <summary>Menu: "Remove triggered alerts" is shown.</summary>
	public bool HasTriggeredAlerts => Alerts.Any(x => x.IsTriggered);

	/// <summary>The last bar's close: alerts are told up or down from it. Null without bars.</summary>
	public double? LastPrice => Bars.Count > 0 ? Bars[^1].Close : null;

	/// <summary>An alert can be added: a contract with bars (for the direction).</summary>
	public bool CanAddAlert => _contract != null && LastPrice != null;

	/// <summary>Adds an alert at the price; a refusal (limits) shows on the chart.</summary>
	public void AddAlert(double? price)
	{
		if (price is not { } level || !CanAddAlert)
			return;
		try
		{
			_priceAlertManager.Create(
				new CreatePriceAlertRequest
				{
					Contract = _contract,
					Price = level,
					CurrentPrice = LastPrice,
				}
			);
			LoadAlerts();
		}
		catch (LSCoreBadRequestException ex)
		{
			ErrorMessage = ex.Message;
		}
	}

	/// <summary>A dragged alert line was dropped at a new level.</summary>
	public void MoveAlert(string id, double price)
	{
		try
		{
			_priceAlertManager.Move(
				new MovePriceAlertRequest
				{
					Id = id,
					Price = price,
					CurrentPrice = LastPrice,
				}
			);
		}
		catch (LSCoreBadRequestException ex)
		{
			ErrorMessage = ex.Message;
		}
		finally
		{
			// Now: the dropped line stays at its new level, or a refused one goes back at once
			LoadAlerts();
		}
	}

	public void RemoveAlert(string id)
	{
		_priceAlertManager.Delete(id);
		LoadAlerts();
	}

	[RelayCommand]
	public void RemoveTriggeredAlerts()
	{
		if (_contract is { } contract)
			_priceAlertManager.DeleteTriggered(contract.ConId);
	}

	// From any thread (a price stream fired one, another chart moved one); one UI update per burst
	private void OnAlertsChanged()
	{
		if (Interlocked.Exchange(ref _alertsQueued, 1) == 0)
			Dispatcher.UIThread.Post(() =>
			{
				Interlocked.Exchange(ref _alertsQueued, 0);
				if (!_disposed)
					LoadAlerts();
			});
	}

	private void LoadAlerts() =>
		Alerts = _contract is { } contract ? _priceAlertManager.GetAlerts(contract.ConId) : [];
}
