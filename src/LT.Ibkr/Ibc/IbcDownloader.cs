using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Gateway;
using LT.Contracts.Interfaces.IProcesses;

namespace LT.Ibkr.Ibc;

/// <summary>Fetches IBC releases from GitHub (IbcAlpha/IBC) and installs the zip.</summary>
public class IbcDownloader : IIbcDownloader
{
	private static readonly HttpClient Http = CreateHttpClient();

	private const UnixFileMode ExecutableMode =
		UnixFileMode.UserRead
		| UnixFileMode.UserWrite
		| UnixFileMode.UserExecute
		| UnixFileMode.GroupRead
		| UnixFileMode.GroupExecute
		| UnixFileMode.OtherRead
		| UnixFileMode.OtherExecute;

	public async Task<IbcReleaseDto> GetLatestReleaseAsync(
		CancellationToken cancellationToken = default
	)
	{
		using var response = await Http.GetAsync(
			GatewayConstants.IbcLatestReleaseUrl,
			cancellationToken
		);
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var json = await JsonDocument.ParseAsync(
			stream,
			cancellationToken: cancellationToken
		);

		var root = json.RootElement;
		return new IbcReleaseDto
		{
			Version = root.GetProperty("tag_name").GetString() ?? string.Empty,
			Assets = root.GetProperty("assets")
				.EnumerateArray()
				.Select(x => new IbcReleaseAssetDto
				{
					Name = x.GetProperty("name").GetString() ?? string.Empty,
					DownloadUrl = x.GetProperty("browser_download_url").GetString() ?? string.Empty,
					Size = x.GetProperty("size").GetInt64(),
				})
				.ToList(),
		};
	}

	public async Task InstallAsync(
		IbcReleaseAssetDto asset,
		string targetDirectory,
		CancellationToken cancellationToken = default
	)
	{
		var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(targetDirectory))!;
		Directory.CreateDirectory(parent);
		var zipPath = Path.Combine(parent, $"ibc-download-{Guid.NewGuid():N}.zip");
		var staging = Path.Combine(parent, $"ibc-staging-{Guid.NewGuid():N}");
		try
		{
			await DownloadAsync(asset.DownloadUrl, zipPath, cancellationToken);

			// ExtractToDirectory rejects entries that would land outside the folder
			ZipFile.ExtractToDirectory(zipPath, staging);
			if (!OperatingSystem.IsWindows())
				foreach (
					var script in Directory.EnumerateFiles(
						staging,
						"*.sh",
						SearchOption.AllDirectories
					)
				)
					File.SetUnixFileMode(script, ExecutableMode);

			if (Directory.Exists(targetDirectory))
				Directory.Delete(targetDirectory, recursive: true);
			Directory.Move(staging, targetDirectory);
		}
		finally
		{
			File.Delete(zipPath);
			if (Directory.Exists(staging))
				Directory.Delete(staging, recursive: true);
		}
	}

	private static async Task DownloadAsync(
		string url,
		string path,
		CancellationToken cancellationToken
	)
	{
		using var response = await Http.GetAsync(
			url,
			HttpCompletionOption.ResponseHeadersRead,
			cancellationToken
		);
		response.EnsureSuccessStatusCode();
		if (response.Content.Headers.ContentLength > GatewayConstants.IbcMaxDownloadBytes)
			throw new InvalidOperationException("IBC download is unexpectedly large.");

		await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
		await using var target = File.Create(path);
		var buffer = new byte[81920];
		long total = 0;
		int read;
		while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
		{
			total += read;
			if (total > GatewayConstants.IbcMaxDownloadBytes)
				throw new InvalidOperationException("IBC download is unexpectedly large.");
			await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
		}
	}

	private static HttpClient CreateHttpClient()
	{
		var client = new HttpClient { Timeout = GatewayConstants.IbcDownloadTimeout };
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
