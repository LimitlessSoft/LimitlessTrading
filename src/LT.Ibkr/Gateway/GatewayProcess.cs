using System.Diagnostics;
using System.Net.Sockets;
using LT.Contracts.Constants;
using LT.Contracts.Interfaces.IProcesses;

namespace LT.Ibkr.Gateway;

/// <summary>
/// Runs IB Gateway's own launcher as a child process; the user logs in in the window it opens.
/// Holds at most one process; registered as a singleton.
/// </summary>
public class GatewayProcess : IGatewayProcess
{
	private readonly Lock _lock = new();
	private Process? _process;

	public bool IsRunning
	{
		get
		{
			lock (_lock)
				return _process is { HasExited: false };
		}
	}

	public event Action<int>? Exited;

	public void Start(string launcherPath)
	{
		lock (_lock)
		{
			if (_process is { HasExited: false })
				throw new InvalidOperationException("IB Gateway is already running.");
			_process?.Dispose();
			_process = null;

			// Gateway logs to stdout; read it away so it neither fills the pipe nor the app's
			// console. The Linux launcher execs java, so this process is Gateway itself.
			var startInfo = new ProcessStartInfo(launcherPath)
			{
				UseShellExecute = false,
				WorkingDirectory = Path.GetDirectoryName(launcherPath) ?? string.Empty,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
			process.OutputDataReceived += (_, _) => { };
			process.ErrorDataReceived += (_, _) => { };
			process.Exited += (_, _) => Exited?.Invoke(process.ExitCode);

			try
			{
				process.Start();
			}
			catch
			{
				process.Dispose();
				throw;
			}
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();
			_process = process;
		}
	}

	public async Task<bool> IsPortOpenAsync(int port, CancellationToken cancellationToken = default)
	{
		using var client = new TcpClient();
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(GatewayConstants.PortProbeTimeout);
		try
		{
			await client.ConnectAsync(ConnectionConstants.DefaultHost, port, timeout.Token);
			return true;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return false;
		}
		catch (SocketException)
		{
			return false;
		}
	}

	public async Task StopAsync()
	{
		Process? process;
		lock (_lock)
			process = _process;
		if (process == null)
			return;

		// Gateway has no stop command or API to close it gracefully
		try
		{
			if (!process.HasExited)
				process.Kill(entireProcessTree: true);
		}
		catch (InvalidOperationException)
		{
			// Exited in the meantime
		}
		await process.WaitForExitAsync();

		lock (_lock)
			if (_process == process)
				_process = null;
		process.Dispose();
	}
}
