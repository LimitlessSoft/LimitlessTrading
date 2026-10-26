using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LT.Contracts.Constants;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IProcesses;

namespace LT.App.Services;

/// <summary>
/// Plays sounds with what the system already has, so the app carries no audio library: winmm's
/// PlaySound on Windows, afplay on macOS, and on Linux paplay (PulseAudio and PipeWire), pw-play
/// or aplay (plain ALSA), whichever works first. The sound is a WAV generated once into the app
/// data folder (<see cref="SoundHelpers"/>). Everything runs off the calling thread; a sound
/// asked for while the previous one still plays is skipped. Singleton (Program.cs).
/// </summary>
public class SoundPlayer : ISoundPlayer
{
	private static readonly (string FileName, string[] Arguments)[] LinuxPlayers =
	[
		("paplay", []),
		("pw-play", []),
		("aplay", ["-q"]),
	];

	private int _playing;
	private string? _fillPath;

	// The Linux player that worked last; tried first next time
	private int _linuxPlayer;

	public void PlayFill()
	{
		if (Interlocked.Exchange(ref _playing, 1) == 1)
			return;
		_ = Task.Run(async () =>
		{
			try
			{
				_fillPath ??= WriteSound(
					SoundConstants.FillFileName,
					SoundHelpers.CreateFillChime()
				);
				if (_fillPath != null)
					await PlayFileAsync(_fillPath);
			}
			catch (Exception)
			{
				// No audio device, player or folder: no sound, nothing else
			}
			finally
			{
				Volatile.Write(ref _playing, 0);
			}
		});
	}

	private async Task PlayFileAsync(string path)
	{
		if (OperatingSystem.IsWindows())
		{
			// Returns at once and plays on winmm's own thread
			PlaySound(path, IntPtr.Zero, SndAsync | SndNoDefault | SndFilename);
			return;
		}
		if (OperatingSystem.IsMacOS())
		{
			await RunPlayerAsync("afplay", [path]);
			return;
		}
		for (var i = 0; i < LinuxPlayers.Length; i++)
		{
			var index = (_linuxPlayer + i) % LinuxPlayers.Length;
			var (fileName, arguments) = LinuxPlayers[index];
			if (!await RunPlayerAsync(fileName, [.. arguments, path]))
				continue;
			_linuxPlayer = index;
			return;
		}
	}

	// False when the player isn't installed or failed (e.g. paplay without a sound server)
	private static async Task<bool> RunPlayerAsync(string fileName, string[] arguments)
	{
		var info = new ProcessStartInfo(fileName)
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		foreach (var argument in arguments)
			info.ArgumentList.Add(argument);

		Process? process;
		try
		{
			process = Process.Start(info);
		}
		catch (Win32Exception)
		{
			return false;
		}
		if (process == null)
			return false;

		using (process)
		{
			// Read, so a chatty player never blocks on a full pipe
			var output = process.StandardOutput.ReadToEndAsync();
			var error = process.StandardError.ReadToEndAsync();
			using var timeout = new CancellationTokenSource(SoundConstants.PlayerTimeout);
			try
			{
				await process.WaitForExitAsync(timeout.Token);
			}
			catch (OperationCanceledException)
			{
				process.Kill(true);
				return true;
			}
			await Task.WhenAll(output, error);
			return process.ExitCode == 0;
		}
	}

	// The generated sound in the app data folder, rewritten only when it differs; null when it
	// can't be written
	private static string? WriteSound(string fileName, byte[] wav)
	{
		try
		{
			var directory = AppPathHelpers.GetSoundsDirectory();
			Directory.CreateDirectory(directory);
			var path = Path.Combine(directory, fileName);
			if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(wav))
				return path;
			// Written aside and moved in, so a second app instance never plays half a file
			var temporary = $"{path}.{Environment.ProcessId}.tmp";
			File.WriteAllBytes(temporary, wav);
			File.Move(temporary, path, true);
			return path;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private const uint SndAsync = 0x0001;
	private const uint SndNoDefault = 0x0002;
	private const uint SndFilename = 0x00020000;

	[DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode)]
	private static extern bool PlaySound(string sound, IntPtr module, uint flags);
}
