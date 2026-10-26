using LT.Contracts.Dtos.Executions;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IProcesses;

namespace LT.Domain.Managers;

/// <summary>
/// Singleton (Program.cs): remembers every ExecId seen while the app runs, so a fill IB returns
/// again on the next request or after a reconnect never sounds twice. Fills made while
/// disconnected come in with the reconnect's replay and stay silent.
/// </summary>
public class FillAlertManager(ISettingsManager settingsManager, ISoundPlayer soundPlayer)
	: IFillAlertManager
{
	private readonly Lock _lock = new();
	private readonly HashSet<string> _seen = [];
	private bool _replayPending = true;

	public void BeginSession()
	{
		lock (_lock)
			_replayPending = true;
	}

	public int OnExecutions(IReadOnlyList<ExecutionDto> executions)
	{
		int added;
		bool replay;
		lock (_lock)
		{
			added = FillAlertHelpers.AddNewExecutions(_seen, executions);
			replay = _replayPending;
			_replayPending = false;
		}
		if (replay || added == 0)
			return 0;

		// One sound for every fill of one answer: players started together would only overlap
		try
		{
			if (settingsManager.GetAppSettings().PlayFillSound)
				soundPlayer.PlayFill();
		}
		catch (Exception)
		{
			// A sound is never worth failing the journal over
		}
		return added;
	}
}
