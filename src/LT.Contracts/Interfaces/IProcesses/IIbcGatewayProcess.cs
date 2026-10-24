using LT.Contracts.Dtos.Gateway;

namespace LT.Contracts.Interfaces.IProcesses;

/// <summary>
/// The IBC + IB Gateway child process. Implemented in LT.Ibkr; registered as a singleton.
/// </summary>
public interface IIbcGatewayProcess
{
	bool IsRunning { get; }

	/// <summary>Raised on a background thread for every stdout / stderr line.</summary>
	event Action<string>? OutputReceived;

	/// <summary>Raised on a background thread with the exit code.</summary>
	event Action<int>? Exited;

	bool ScriptExists(string path);

	/// <summary>Writes the config file (owner-only) and starts the process.</summary>
	void Start(IbcLaunchDto launch);

	/// <summary>Deletes the config file holding the password. Safe to call repeatedly.</summary>
	void DeleteConfig();

	Task<bool> IsPortOpenAsync(int port, CancellationToken cancellationToken = default);

	/// <summary>Asks IBC to STOP via its command server, kills the process tree on timeout.</summary>
	Task StopAsync();
}
