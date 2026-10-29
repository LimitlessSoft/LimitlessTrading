using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Feeds;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Exceptions;
using LT.Contracts.Interfaces.IApiClients;

namespace LT.Feeds;

/// <summary>
/// The app's news feeds over HTTPS with one HttpClient. Requests to one host go one at a time,
/// <see cref="FeedNewsConstants.HostRequestSpacing"/> apart (several SEC feeds stay within its 10
/// a second); conditional GET with the last ETag / Last-Modified. Singleton (Program.cs).
/// </summary>
public sealed class FeedClient(HttpClient http, TimeProvider timeProvider) : IFeedClient
{
	private readonly Lock _lock = new();
	private readonly Dictionary<string, HostGate> _hosts = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The HttpClient registered for this client: timeout, gzip, the contact User-Agent and a feed Accept.</summary>
	public static HttpClient CreateHttpClient(HttpMessageHandler? handler = null)
	{
		var client = new HttpClient(
			handler ?? new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All }
		)
		{
			Timeout = FeedNewsConstants.HttpTimeout,
		};
		var version = typeof(FeedClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
		client.DefaultRequestHeaders.TryAddWithoutValidation(
			"User-Agent",
			FeedNewsConstants.UserAgent(version)
		);
		client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", FeedNewsConstants.Accept);
		return client;
	}

	public async Task<FeedResponseDto> GetAsync(
		string url,
		FeedConditionsDto? conditions = null,
		CancellationToken cancellationToken = default
	)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, url);
		if (conditions?.ETag is { } etag && EntityTagHeaderValue.TryParse(etag, out var tag))
			request.Headers.IfNoneMatch.Add(tag);
		if (conditions?.LastModified is { } modified)
			request.Headers.IfModifiedSince = modified;

		return await SendAsync(
			url,
			request,
			async response =>
			{
				if (response.StatusCode == HttpStatusCode.NotModified)
					return new FeedResponseDto
					{
						NotModified = true,
						Conditions = conditions ?? new(),
					};
				EnsureSuccess(url, response);
				await using var stream = await response.Content.ReadAsStreamAsync(
					cancellationToken
				);
				FeedResponseDto feed;
				try
				{
					feed = FeedParser.Parse(stream);
				}
				catch (XmlException ex)
				{
					throw new FeedServiceException(
						Message(url, "not a feed (" + ex.Message + ")"),
						ex
					);
				}
				feed.Conditions = new FeedConditionsDto
				{
					ETag = response.Headers.ETag?.ToString(),
					LastModified = response.Content.Headers.LastModified,
				};
				return feed;
			},
			cancellationToken
		);
	}

	public Task<string> GetTextAsync(string url, CancellationToken cancellationToken = default) =>
		SendAsync(
			url,
			new HttpRequestMessage(HttpMethod.Get, url),
			async response =>
			{
				EnsureSuccess(url, response);
				if (response.Content.Headers.ContentLength > FeedNewsConstants.MaxArticleBytes)
					throw new FeedServiceException(Message(url, "the page is too large"));
				await using var stream = await response.Content.ReadAsStreamAsync(
					cancellationToken
				);
				var buffer = new byte[FeedNewsConstants.MaxArticleBytes];
				var length = 0;
				int read;
				while (
					length < buffer.Length
					&& (read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken))
						> 0
				)
					length += read;
				var charset = response.Content.Headers.ContentType?.CharSet;
				var encoding = Encoding.UTF8;
				try
				{
					if (!string.IsNullOrEmpty(charset))
						encoding = Encoding.GetEncoding(charset.Trim('"'));
				}
				catch (ArgumentException) { }
				return encoding.GetString(buffer, 0, length);
			},
			cancellationToken
		);

	private async Task<T> SendAsync<T>(
		string url,
		HttpRequestMessage request,
		Func<HttpResponseMessage, Task<T>> read,
		CancellationToken cancellationToken
	)
	{
		var gate = GetGate(request.RequestUri!.Host);
		await gate.Semaphore.WaitAsync(cancellationToken);
		try
		{
			var wait =
				gate.LastRequest + FeedNewsConstants.HostRequestSpacing - timeProvider.GetUtcNow();
			if (wait > TimeSpan.Zero)
				await Task.Delay(wait, timeProvider, cancellationToken);
			gate.LastRequest = timeProvider.GetUtcNow();

			using var response = await http.SendAsync(
				request,
				HttpCompletionOption.ResponseHeadersRead,
				cancellationToken
			);
			return await read(response);
		}
		catch (HttpRequestException ex)
		{
			throw new FeedServiceException(Message(url, ex.Message), ex);
		}
		catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
		{
			throw new FeedServiceException(
				Message(
					url,
					$"no answer within {(int)FeedNewsConstants.HttpTimeout.TotalSeconds} s"
				),
				ex
			);
		}
		finally
		{
			request.Dispose();
			gate.Semaphore.Release();
		}
	}

	private HostGate GetGate(string host)
	{
		lock (_lock)
		{
			if (!_hosts.TryGetValue(host, out var gate))
				_hosts[host] = gate = new HostGate();
			return gate;
		}
	}

	private static void EnsureSuccess(string url, HttpResponseMessage response)
	{
		if (!response.IsSuccessStatusCode)
			throw new FeedServiceException(
				Message(url, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".Trim())
			);
	}

	private static string Message(string url, string reason) =>
		NewsValidationCodes.NVC_008.GetValidationMessage(
			Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url,
			reason
		);

	private sealed class HostGate
	{
		public SemaphoreSlim Semaphore { get; } = new(1, 1);
		public DateTimeOffset LastRequest { get; set; } = DateTimeOffset.MinValue;
	}
}
