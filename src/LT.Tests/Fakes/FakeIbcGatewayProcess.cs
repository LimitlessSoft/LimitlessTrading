using LT.Contracts.Dtos.Gateway;
using LT.Contracts.Interfaces.IProcesses;

namespace LT.Tests.Fakes;

/// <summary>
/// Scripted IBC: <see cref="OnStart"/> runs right after Start, the API port reports open once
/// <see cref="PortOpensAfterProbes"/> probes have been made after start.
/// </summary>
public class FakeIbcGatewayProcess : IIbcGatewayProcess
{
	private int _probesSinceStart;

	public bool IsRunning { get; set; }
	public bool ScriptPresent { get; set; } = true;
	public bool PortOpenBeforeStart { get; set; }
	public int? PortOpensAfterProbes { get; set; } = 0;
	public Action<FakeIbcGatewayProcess>? OnStart { get; set; }
	public IbcLaunchDto? LastLaunch { get; private set; }
	public int StopCount { get; private set; }
	public int DeleteConfigCount { get; private set; }

	public event Action<string>? OutputReceived;
	public event Action<int>? Exited;

	public bool ScriptExists(string path) => ScriptPresent;

	public void Start(IbcLaunchDto launch)
	{
		LastLaunch = launch;
		IsRunning = true;
		_probesSinceStart = 0;
		OnStart?.Invoke(this);
	}

	public void DeleteConfig() => DeleteConfigCount++;

	public Task<bool> IsPortOpenAsync(int port, CancellationToken cancellationToken = default)
	{
		if (LastLaunch == null || !IsRunning)
			return Task.FromResult(PortOpenBeforeStart);
		return Task.FromResult(PortOpensAfterProbes is { } after && _probesSinceStart++ >= after);
	}

	public Task StopAsync()
	{
		StopCount++;
		IsRunning = false;
		return Task.CompletedTask;
	}

	public void EmitOutput(string line) => OutputReceived?.Invoke(line);

	public void Exit(int exitCode)
	{
		IsRunning = false;
		Exited?.Invoke(exitCode);
	}
}
