using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Gateway;
using LT.Contracts.Interfaces.IProcesses;

namespace LT.Ibkr.Ibc;

/// <summary>
/// Runs IBC (which runs IB Gateway) as a child process. Holds at most one process; registered as
/// a singleton.
/// </summary>
public class IbcGatewayProcess : IIbcGatewayProcess
{
	private readonly Lock _lock = new();
	private Process? _process;
	private string? _configFilePath;

	public bool IsRunning
	{
		get
		{
			lock (_lock)
				return _process is { HasExited: false };
		}
	}

	public event Action<string>? OutputReceived;
	public event Action<int>? Exited;

	public bool ScriptExists(string path) => File.Exists(path);

	public void Start(IbcLaunchDto launch)
	{
		lock (_lock)
		{
			if (_process is { HasExited: false })
				throw new InvalidOperationException("IBC is already running.");
			_process?.Dispose();
			_process = null;

			WriteConfig(launch.ConfigFilePath, launch.ConfigContent);
			_configFilePath = launch.ConfigFilePath;

			var startInfo = new ProcessStartInfo(launch.FileName)
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			foreach (var argument in launch.Arguments)
				startInfo.ArgumentList.Add(argument);

			var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
			process.OutputDataReceived += (_, e) => OnOutput(e.Data);
			process.ErrorDataReceived += (_, e) => OnOutput(e.Data);
			process.Exited += (_, _) => Exited?.Invoke(process.ExitCode);

			try
			{
				process.Start();
			}
			catch
			{
				DeleteConfigFile();
				process.Dispose();
				throw;
			}
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();
			_process = process;
		}
	}

	public void DeleteConfig()
	{
		lock (_lock)
			DeleteConfigFile();
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

		if (!process.HasExited)
		{
			await SendStopCommandAsync();
			using var timeout = new CancellationTokenSource(GatewayConstants.StopTimeout);
			try
			{
				await process.WaitForExitAsync(timeout.Token);
			}
			catch (OperationCanceledException)
			{
				process.Kill(entireProcessTree: true);
				await process.WaitForExitAsync();
			}
		}

		lock (_lock)
		{
			DeleteConfigFile();
			if (_process == process)
				_process = null;
		}
		process.Dispose();
	}

	private void OnOutput(string? line)
	{
		if (line != null)
			OutputReceived?.Invoke(line);
	}

	/// <summary>IBC's command server; fails silently if it isn't listening (e.g. still starting).</summary>
	private static async Task SendStopCommandAsync()
	{
		try
		{
			using var client = new TcpClient();
			using var timeout = new CancellationTokenSource(GatewayConstants.PortProbeTimeout * 4);
			await client.ConnectAsync(
				ConnectionConstants.DefaultHost,
				GatewayConstants.IbcCommandServerPort,
				timeout.Token
			);
			await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes("STOP\n"), timeout.Token);
		}
		catch (Exception ex)
			when (ex is SocketException or OperationCanceledException or IOException)
		{
			// Falls through to the kill after StopTimeout
		}
	}

	private static void WriteConfig(string path, string content)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.Delete(path);
		var options = new FileStreamOptions
		{
			Mode = FileMode.CreateNew,
			Access = FileAccess.Write,
		};
		// Windows: the per-user app-data folder ACL already keeps other users out
		if (!OperatingSystem.IsWindows())
			options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
		// No BOM: IBC would read it as part of the first key
		using var writer = new StreamWriter(path, new UTF8Encoding(false), options);
		writer.Write(content);
	}

	private void DeleteConfigFile()
	{
		if (_configFilePath == null)
			return;
		try
		{
			File.Delete(_configFilePath);
			_configFilePath = null;
		}
		catch (IOException)
		{
			// Retried on the next DeleteConfig / Stop
		}
	}
}
