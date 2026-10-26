namespace LT.Contracts.Interfaces.IProcesses;

/// <summary>
/// The IB Gateway child process, started from its own launcher so the user logs in in Gateway's
/// window. Implemented in LT.Ibkr; registered as a singleton.
/// </summary>
public interface IGatewayProcess
{
	bool IsRunning { get; }

	/// <summary>Raised on a background thread with the exit code.</summary>
	event Action<int>? Exited;

	/// <summary>Starts the launcher (e.g. ibgateway/1050/ibgateway) in its own folder.</summary>
	void Start(string launcherPath);

	Task<bool> IsPortOpenAsync(int port, CancellationToken cancellationToken = default);

	/// <summary>Kills the process tree started by <see cref="Start"/>; no-op when there is none.</summary>
	Task StopAsync();
}
