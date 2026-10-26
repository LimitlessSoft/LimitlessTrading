using LT.Contracts.Interfaces.IProcesses;

namespace LT.Tests.Fakes;

/// <summary>
/// Scripted Gateway: <see cref="OnStart"/> runs right after Start; after start the port in
/// <see cref="PortAfterLogin"/> reports open once <see cref="PortOpensAfterProbes"/> probes of it
/// have been made (null: never, the user never logs in).
/// </summary>
public class FakeGatewayProcess : IGatewayProcess
{
	private int _probesSinceStart;

	public bool IsRunning { get; set; }
	public HashSet<int> PortsOpenBeforeStart { get; } = [];
	public int PortAfterLogin { get; set; } = 4002;
	public int? PortOpensAfterProbes { get; set; } = 0;
	public Action<FakeGatewayProcess>? OnStart { get; set; }
	public string? LastLauncher { get; private set; }
	public int StopCount { get; private set; }

	public event Action<int>? Exited;

	public void Start(string launcherPath)
	{
		LastLauncher = launcherPath;
		IsRunning = true;
		_probesSinceStart = 0;
		OnStart?.Invoke(this);
	}

	public Task<bool> IsPortOpenAsync(int port, CancellationToken cancellationToken = default)
	{
		if (LastLauncher == null || !IsRunning)
			return Task.FromResult(PortsOpenBeforeStart.Contains(port));
		return Task.FromResult(
			port == PortAfterLogin
				&& PortOpensAfterProbes is { } after
				&& _probesSinceStart++ >= after
		);
	}

	public Task StopAsync()
	{
		StopCount++;
		IsRunning = false;
		return Task.CompletedTask;
	}

	public void Exit(int exitCode)
	{
		IsRunning = false;
		Exited?.Invoke(exitCode);
	}
}
