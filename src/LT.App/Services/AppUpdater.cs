using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Updates;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IProcesses;

namespace LT.App.Services;

/// <summary>
/// Replaces this copy of the app with a GitHub release. The release archive holds a
/// LimitlessTrading/ folder; it is unpacked next to the app folder, the two folders are swapped
/// (Linux lets a running app's folder be renamed) and a small shell waits for this process to
/// exit before starting the new one, since IB rejects a second session with the same client id.
/// Linux release builds only for now.
/// </summary>
public class AppUpdater : IAppUpdater
{
	private static readonly HttpClient Http = CreateHttpClient();

	// The archive's top folder and the executable inside it (see .github/workflows/release.yml)
	private const string ArchiveFolderName = "LimitlessTrading";
	private const string ExecutableName = "LimitlessTrading";

	// The app folder without its trailing separator, e.g. /opt/LimitlessTrading
	private readonly string _installDirectory = Path.TrimEndingDirectorySeparator(
		AppContext.BaseDirectory
	);

	public AppUpdater()
	{
		CurrentVersion =
			Assembly
				.GetEntryAssembly()
				?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
				?.InformationalVersion
			?? "0.0.0";
		CannotInstallReason = GetCannotInstallReason();
	}

	public string CurrentVersion { get; }

	public string AssetSuffix => "-linux-x64.tar.gz";

	public string? CannotInstallReason { get; }

	// Siblings of the app folder, hidden: .LimitlessTrading.update and .LimitlessTrading.old
	private string StagingDirectory => SiblingDirectory("update");
	private string PreviousDirectory => SiblingDirectory("old");

	public async Task<AppReleaseDto> GetLatestReleaseAsync(
		CancellationToken cancellationToken = default
	)
	{
		using var response = await Http.GetAsync(
			AppConstants.LatestReleaseApiUrl,
			cancellationToken
		);
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var json = await JsonDocument.ParseAsync(
			stream,
			cancellationToken: cancellationToken
		);

		return ReadRelease(json.RootElement);
	}

	public async Task<List<AppReleaseDto>> GetReleasesAsync(
		CancellationToken cancellationToken = default
	)
	{
		using var response = await Http.GetAsync(AppConstants.ReleasesApiUrl, cancellationToken);
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var json = await JsonDocument.ParseAsync(
			stream,
			cancellationToken: cancellationToken
		);
		// Drafts are only listed to the repository's members; never shown as released
		return json
			.RootElement.EnumerateArray()
			.Where(x => !(x.TryGetProperty("draft", out var draft) && draft.GetBoolean()))
			.Select(ReadRelease)
			.ToList();
	}

	public async Task DownloadAsync(
		AppReleaseAssetDto asset,
		IProgress<double>? progress = null,
		CancellationToken cancellationToken = default
	)
	{
		var staging = StagingDirectory;
		if (Directory.Exists(staging))
			Directory.Delete(staging, recursive: true);
		Directory.CreateDirectory(staging);
		var archivePath = Path.Combine(staging, "release.tar.gz");
		try
		{
			var sha256 = await DownloadFileAsync(asset, archivePath, progress, cancellationToken);
			if (!UpdateHelpers.MatchesDigest(asset.Digest, sha256))
				throw new InvalidOperationException(
					UpdateValidationCodes.UVC_005.GetValidationMessage()
				);

			// ExtractToDirectory rejects entries that would land outside the folder and keeps
			// the files' Unix modes (the executable bit)
			await using (var archive = File.OpenRead(archivePath))
			await using (var tar = new GZipStream(archive, CompressionMode.Decompress))
				await TarFile.ExtractToDirectoryAsync(
					tar,
					staging,
					overwriteFiles: false,
					cancellationToken
				);
			File.Delete(archivePath);

			var executable = Path.Combine(staging, ArchiveFolderName, ExecutableName);
			if (!File.Exists(executable))
				throw new InvalidOperationException(
					UpdateValidationCodes.UVC_006.GetValidationMessage(
						$"{ArchiveFolderName}/{ExecutableName}"
					)
				);
		}
		catch
		{
			Directory.Delete(staging, recursive: true);
			throw;
		}
	}

	public void ApplyAndRestart()
	{
		var downloaded = Path.Combine(StagingDirectory, ArchiveFolderName);
		var previous = PreviousDirectory;
		if (Directory.Exists(previous))
			Directory.Delete(previous, recursive: true);

		Directory.Move(_installDirectory, previous);
		try
		{
			Directory.Move(downloaded, _installDirectory);
		}
		catch
		{
			Directory.Move(previous, _installDirectory);
			throw;
		}
		Directory.Delete(StagingDirectory, recursive: true);

		// $1 = this process, $2 = the new executable; the shell outlives us and starts it once
		// we've exited (and disconnected from IB). An exited process stays a zombie until its
		// parent reaps it, so "gone or state Z" counts as exited (kill -0 would wait forever)
		var start = new ProcessStartInfo("/bin/sh") { UseShellExecute = false };
		start.ArgumentList.Add("-c");
		start.ArgumentList.Add(
			"while [ -e \"/proc/$1\" ] && ! grep -q '^[^)]*) Z' \"/proc/$1/stat\" 2>/dev/null;"
				+ " do sleep 0.5; done; exec \"$2\""
		);
		start.ArgumentList.Add("lt-update");
		start.ArgumentList.Add(Environment.ProcessId.ToString());
		start.ArgumentList.Add(Path.Combine(_installDirectory, ExecutableName));
		Process.Start(start)?.Dispose();
	}

	public void CleanUp()
	{
		if (CannotInstallReason != null)
			return;
		foreach (var directory in new[] { PreviousDirectory, StagingDirectory })
			try
			{
				if (Directory.Exists(directory))
					Directory.Delete(directory, recursive: true);
			}
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
	}

	private string SiblingDirectory(string suffix) =>
		Path.Combine(
			Path.GetDirectoryName(_installDirectory)!,
			$".{Path.GetFileName(_installDirectory)}.{suffix}"
		);

	private string? GetCannotInstallReason()
	{
		if (UpdateHelpers.IsDevVersion(CurrentVersion))
			return UpdateValidationCodes.UVC_001.GetValidationMessage();
		// The release's own launcher, not `dotnet LimitlessTrading.dll` or a debugger host
		if (
			!OperatingSystem.IsLinux()
			|| Environment.ProcessPath is not { } processPath
			|| Path.GetFileName(processPath) != ExecutableName
			|| Path.GetDirectoryName(processPath) != _installDirectory
		)
			return UpdateValidationCodes.UVC_002.GetValidationMessage();

		var parent = Path.GetDirectoryName(_installDirectory)!;
		try
		{
			// Swapping needs the parent folder writable, not only the app folder
			var probe = Path.Combine(
				parent,
				$".{ExecutableName}.write-test-{Environment.ProcessId}"
			);
			File.WriteAllBytes(probe, []);
			File.Delete(probe);
			return null;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return UpdateValidationCodes.UVC_003.GetValidationMessage(parent);
		}
	}

	// Streams to the file with progress and the size cap; returns the SHA-256
	private static async Task<byte[]> DownloadFileAsync(
		AppReleaseAssetDto asset,
		string path,
		IProgress<double>? progress,
		CancellationToken cancellationToken
	)
	{
		using var response = await Http.GetAsync(
			asset.DownloadUrl,
			HttpCompletionOption.ResponseHeadersRead,
			cancellationToken
		);
		response.EnsureSuccessStatusCode();
		var length = response.Content.Headers.ContentLength ?? asset.Size;
		if (length > AppConstants.UpdateMaxDownloadBytes)
			throw new InvalidOperationException("The update download is unexpectedly large.");

		using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
		await using var target = File.Create(path);
		var buffer = new byte[81920];
		long total = 0;
		int read;
		while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
		{
			total += read;
			if (total > AppConstants.UpdateMaxDownloadBytes)
				throw new InvalidOperationException("The update download is unexpectedly large.");
			hash.AppendData(buffer, 0, read);
			await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
			if (length > 0)
				progress?.Report(Math.Min(1, (double)total / length));
		}
		return hash.GetHashAndReset();
	}

	// One release of GitHub's REST API
	private static AppReleaseDto ReadRelease(JsonElement release) =>
		new()
		{
			Tag = release.GetProperty("tag_name").GetString() ?? string.Empty,
			PageUrl = release.GetProperty("html_url").GetString() ?? string.Empty,
			PublishedAt =
				release.TryGetProperty("published_at", out var published)
				&& published.ValueKind == JsonValueKind.String
					? published.GetDateTimeOffset()
					: null,
			Body =
				release.TryGetProperty("body", out var body)
				&& body.ValueKind == JsonValueKind.String
					? body.GetString() ?? string.Empty
					: string.Empty,
			Assets = release
				.GetProperty("assets")
				.EnumerateArray()
				.Select(x => new AppReleaseAssetDto
				{
					Name = x.GetProperty("name").GetString() ?? string.Empty,
					DownloadUrl = x.GetProperty("browser_download_url").GetString() ?? string.Empty,
					Size = x.GetProperty("size").GetInt64(),
					Digest = x.TryGetProperty("digest", out var digest) ? digest.GetString() : null,
				})
				.ToList(),
		};

	private static HttpClient CreateHttpClient()
	{
		var client = new HttpClient { Timeout = AppConstants.UpdateDownloadTimeout };
		// GitHub's API rejects requests without a User-Agent
		client.DefaultRequestHeaders.UserAgent.Add(
			new ProductInfoHeaderValue(AppConstants.AppName, "1.0")
		);
		client.DefaultRequestHeaders.Accept.Add(
			new MediaTypeWithQualityHeaderValue("application/vnd.github+json")
		);
		return client;
	}
}
