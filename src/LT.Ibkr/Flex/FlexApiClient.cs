using System.Net.Http.Headers;
using System.Xml.Linq;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Flex;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Requests.Flex;

namespace LT.Ibkr.Flex;

/// <summary>
/// IB's Flex Web Service v3 over HTTPS (no IBKR.Net, no Gateway): <c>SendRequest?t=&amp;q=&amp;v=3</c>
/// (optionally <c>fd</c> / <c>td</c>, yyyyMMdd) answers a ReferenceCode and the statement URL,
/// then <c>GetStatement?t=&amp;q=&lt;ref&gt;&amp;v=3</c> is polled with a growing delay while IB is
/// still generating (1019 and the other "try again shortly" codes) until
/// <see cref="FlexConstants.StatementTimeout"/>. Every HTTP call goes through one gate and two
/// throttles (≥ 1.2 s apart, ≤ 9 per minute — IB allows 1/s and 10/min per token). Singleton
/// (Program.cs) so the limits hold app-wide. The token is only ever put in the query string; it
/// never appears in a message.
/// </summary>
public sealed class FlexApiClient : IFlexApiClient
{
	private readonly HttpClient _http;
	private readonly TimeProvider _timeProvider;
	private readonly TimeZoneInfo _zone = FlexHelpers.GetStatementTimeZone();
	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly RequestThrottle _spacing;
	private readonly RequestThrottle _perMinute;

	public FlexApiClient(HttpClient http, TimeProvider timeProvider)
	{
		_http = http;
		_timeProvider = timeProvider;
		_spacing = new RequestThrottle(1, FlexConstants.RequestSpacing, timeProvider);
		_perMinute = new RequestThrottle(
			FlexConstants.MaxRequestsPerMinute,
			TimeSpan.FromMinutes(1),
			timeProvider
		);
	}

	/// <summary>The HttpClient registered for this client: timeout and the User-Agent IB requires.</summary>
	public static HttpClient CreateHttpClient(HttpMessageHandler? handler = null)
	{
		var client = handler != null ? new HttpClient(handler) : new HttpClient();
		client.Timeout = FlexConstants.HttpTimeout;
		var version = typeof(FlexApiClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
		client.DefaultRequestHeaders.UserAgent.Add(
			new ProductInfoHeaderValue(FlexConstants.UserAgentProduct, version)
		);
		return client;
	}

	public async Task<FlexStatementDto> GetStatementAsync(
		FlexStatementRequest request,
		CancellationToken cancellationToken = default
	)
	{
		using var timeout = new CancellationTokenSource(
			FlexConstants.StatementTimeout,
			_timeProvider
		);
		using var linked = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken,
			timeout.Token
		);
		try
		{
			var (reference, statementUrl) = await SendRequestAsync(request, linked.Token);
			return await PollStatementAsync(request.Token, reference, statementUrl, linked.Token);
		}
		catch (OperationCanceledException)
			when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			throw new FlexServiceException(
				FlexValidationCodes.FVC_015.GetValidationMessage(
					(int)FlexConstants.StatementTimeout.TotalMinutes
				)
			);
		}
	}

	private async Task<(string Reference, string Url)> SendRequestAsync(
		FlexStatementRequest request,
		CancellationToken cancellationToken
	)
	{
		var query =
			$"t={Uri.EscapeDataString(request.Token)}&q={Uri.EscapeDataString(request.QueryId)}";
		if (request.From is { } from)
			query += $"&fd={FlexHelpers.FormatRequestDate(from)}";
		if (request.To is { } to)
			query += $"&td={FlexHelpers.FormatRequestDate(to)}";
		var url =
			$"{FlexConstants.BaseUrl}{FlexConstants.SendRequestPath}?{query}&v={FlexConstants.Version}";

		var delay = FlexConstants.PollInitialDelay;
		while (true)
		{
			var root = await GetXmlAsync(url, cancellationToken);
			var failure = GetFailure(root);
			if (failure == null)
			{
				var reference = root.Element("ReferenceCode")?.Value.Trim();
				if (string.IsNullOrEmpty(reference))
					throw new FlexServiceException(
						FlexValidationCodes.FVC_016.GetValidationMessage()
					);
				return (reference, GetStatementUrl(root.Element("url")?.Value));
			}

			var (code, message) = failure.Value;
			if (!FlexHelpers.IsRetryable(code))
				throw new FlexServiceException(FlexHelpers.GetErrorMessage(code, message), code);
			await DelayAsync(code, ref delay, cancellationToken);
		}
	}

	private async Task<FlexStatementDto> PollStatementAsync(
		string token,
		string reference,
		string statementUrl,
		CancellationToken cancellationToken
	)
	{
		var url =
			$"{statementUrl}?t={Uri.EscapeDataString(token)}&q={Uri.EscapeDataString(reference)}&v={FlexConstants.Version}";
		var delay = FlexConstants.PollInitialDelay;
		// IB needs a few seconds to generate even a small statement
		await Task.Delay(delay, _timeProvider, cancellationToken);
		while (true)
		{
			var root = await GetXmlAsync(url, cancellationToken);
			if (root.Name.LocalName == "FlexQueryResponse")
				return FlexStatementParser.Parse(root.Document!, _zone);

			var failure =
				GetFailure(root)
				?? throw new FlexServiceException(
					FlexValidationCodes.FVC_016.GetValidationMessage()
				);
			var (code, message) = failure;
			if (!FlexHelpers.IsRetryable(code))
				throw new FlexServiceException(FlexHelpers.GetErrorMessage(code, message), code);
			await DelayAsync(code, ref delay, cancellationToken);
		}
	}

	// "Too many requests" waits long; anything else grows the delay up to the maximum
	private Task DelayAsync(int code, ref TimeSpan delay, CancellationToken cancellationToken)
	{
		var wait = code == 1018 ? FlexConstants.TooManyRequestsDelay : delay;
		delay = TimeSpan.FromTicks(
			Math.Min(
				(long)(delay.Ticks * FlexConstants.PollBackoffFactor),
				FlexConstants.PollMaxDelay.Ticks
			)
		);
		return Task.Delay(wait, _timeProvider, cancellationToken);
	}

	private async Task<XElement> GetXmlAsync(string url, CancellationToken cancellationToken)
	{
		string body;
		await _gate.WaitAsync(cancellationToken);
		try
		{
			await _spacing.WaitAsync(cancellationToken);
			await _perMinute.WaitAsync(cancellationToken);
			using var response = await _http.GetAsync(url, cancellationToken);
			response.EnsureSuccessStatusCode();
			body = await response.Content.ReadAsStringAsync(cancellationToken);
		}
		catch (HttpRequestException ex)
		{
			// The message carries the status or the host, never the URL (with the token)
			throw new FlexServiceException(
				FlexValidationCodes.FVC_017.GetValidationMessage(ex.Message),
				inner: ex
			);
		}
		catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
		{
			throw new FlexServiceException(
				FlexValidationCodes.FVC_017.GetValidationMessage(ex.Message)
			);
		}
		finally
		{
			_gate.Release();
		}

		return FlexStatementParser.TryLoad(body)?.Root
			?? throw new FlexServiceException(FlexValidationCodes.FVC_016.GetValidationMessage());
	}

	private static (int Code, string Message)? GetFailure(XElement root)
	{
		var status = root.Element("Status")?.Value.Trim();
		if (string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase))
			return null;
		if (root.Name.LocalName != "FlexStatementResponse")
			throw new FlexServiceException(FlexValidationCodes.FVC_016.GetValidationMessage());
		var code = int.TryParse(root.Element("ErrorCode")?.Value.Trim(), out var parsed)
			? parsed
			: 0;
		return (code, root.Element("ErrorMessage")?.Value.Trim() ?? string.Empty);
	}

	// The token is sent to the URL IB returns only if it is IB's own HTTPS host
	private static string GetStatementUrl(string? returned)
	{
		if (
			Uri.TryCreate(returned?.Trim(), UriKind.Absolute, out var uri)
			&& uri.Scheme == Uri.UriSchemeHttps
			&& uri.Host.EndsWith(
				FlexConstants.AllowedHostSuffix,
				StringComparison.OrdinalIgnoreCase
			)
		)
			return uri.GetLeftPart(UriPartial.Path);
		return FlexConstants.BaseUrl + FlexConstants.GetStatementPath;
	}
}
