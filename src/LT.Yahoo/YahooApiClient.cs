using System.Globalization;
using System.Net;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;

namespace LT.Yahoo;

/// <summary>
/// finance.yahoo.com's earnings calendar page over HTTPS. Every request goes through one gate
/// and stays <see cref="YahooConstants.RequestSpacing"/> after the previous one. Singleton
/// (Program.cs) so the spacing holds app-wide.
/// </summary>
public sealed class YahooApiClient(HttpClient http, TimeProvider timeProvider) : IYahooApiClient
{
	private readonly SemaphoreSlim _gate = new(1, 1);
	private DateTimeOffset _lastRequest = DateTimeOffset.MinValue;

	/// <summary>
	/// The HttpClient registered for this client: timeout, gzip, and the browser User-Agent and
	/// Accept Yahoo wants (without them it pages differently or not at all).
	/// </summary>
	public static HttpClient CreateHttpClient(HttpMessageHandler? handler = null)
	{
		var client = new HttpClient(
			handler ?? new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All }
		)
		{
			Timeout = YahooConstants.HttpTimeout,
		};
		client.DefaultRequestHeaders.TryAddWithoutValidation(
			"User-Agent",
			YahooConstants.UserAgent
		);
		client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", YahooConstants.Accept);
		return client;
	}

	public async Task<YahooEarningsPageDto> GetEarningsPageAsync(
		DateOnly day,
		int offset,
		CancellationToken cancellationToken = default
	)
	{
		var url = string.Format(
			CultureInfo.InvariantCulture,
			YahooConstants.EarningsUrl,
			EarningsResultHelpers.FormatDay(day),
			offset
		);
		return YahooEarningsParser.Parse(await GetStringAsync(url, cancellationToken), day);
	}

	private async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken);
		try
		{
			var wait = _lastRequest + YahooConstants.RequestSpacing - timeProvider.GetUtcNow();
			if (wait > TimeSpan.Zero)
				await Task.Delay(wait, timeProvider, cancellationToken);
			_lastRequest = timeProvider.GetUtcNow();

			using var response = await http.GetAsync(url, cancellationToken);
			if (!response.IsSuccessStatusCode)
				throw new YahooServiceException(
					CalendarValidationCodes.CLVC_011.GetValidationMessage(
						$"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".Trim()
					)
				);
			return await response.Content.ReadAsStringAsync(cancellationToken);
		}
		catch (HttpRequestException ex)
		{
			throw new YahooServiceException(
				CalendarValidationCodes.CLVC_011.GetValidationMessage(ex.Message),
				ex
			);
		}
		catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
		{
			throw new YahooServiceException(
				CalendarValidationCodes.CLVC_011.GetValidationMessage(
					$"no answer within {(int)YahooConstants.HttpTimeout.TotalSeconds} s"
				),
				ex
			);
		}
		finally
		{
			_gate.Release();
		}
	}
}
