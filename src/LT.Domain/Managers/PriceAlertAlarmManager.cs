using LT.Contracts.Constants;
using LT.Contracts.Dtos.PriceAlerts;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IProcesses;

namespace LT.Domain.Managers;

/// <summary>
/// Singleton (Program.cs): the fired alerts not dismissed yet, app-wide, and the one timer that
/// repeats the alarm while any is left. Nothing is saved: an alert still waiting when the app
/// exits stays fired in price-alerts.json, and the next start doesn't ring for it.
/// </summary>
public class PriceAlertAlarmManager(ISoundPlayer soundPlayer, TimeProvider timeProvider)
	: IPriceAlertAlarmManager
{
	private readonly Lock _lock = new();
	private readonly List<PriceAlertDto> _pending = [];
	private ITimer? _timer;

	public event Action? Changed;

	public bool IsRinging
	{
		get
		{
			lock (_lock)
				return _timer != null;
		}
	}

	public IReadOnlyList<PriceAlertDto> GetPending()
	{
		lock (_lock)
			return [.. _pending];
	}

	public void OnTriggered(PriceAlertDto alert)
	{
		lock (_lock)
		{
			_pending.RemoveAll(x => x.Id == alert.Id);
			_pending.Add(alert);
			// Rings at once, then every interval; a timer that fires once more after the last
			// dismissal finds nothing pending and stays silent
			_timer ??= timeProvider.CreateTimer(
				_ =>
				{
					lock (_lock)
						if (_pending.Count == 0)
							return;
					try
					{
						soundPlayer.PlayAlarm();
					}
					catch (Exception)
					{
						// A sound is never worth failing an alert over
					}
				},
				null,
				TimeSpan.Zero,
				PriceAlertConstants.AlarmRepeatInterval
			);
		}
		Changed?.Invoke();
	}

	public void Dismiss(string id)
	{
		lock (_lock)
		{
			if (_pending.RemoveAll(x => x.Id == id) == 0)
				return;
			if (_pending.Count == 0)
			{
				_timer?.Dispose();
				_timer = null;
			}
		}
		Changed?.Invoke();
	}

	public void DismissAll()
	{
		lock (_lock)
		{
			if (_pending.Count == 0)
				return;
			_pending.Clear();
			_timer?.Dispose();
			_timer = null;
		}
		Changed?.Invoke();
	}
}
