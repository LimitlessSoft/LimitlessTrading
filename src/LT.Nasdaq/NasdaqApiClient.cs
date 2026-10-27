using System.Globalization;
using System.Net;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;

namespace LT.Nasdaq;

/// <summary>
/// nasdaq.com's calendars over HTTPS: <c>api.nasdaq.com/api/calendar/&lt;calendar&gt;?date=yyyy-MM-dd</c>.
/// Every request goes through one gate and stays <see cref="NasdaqConstants.RequestSpacing"/>
/// after the previous one. Singleton (Program.cs) so the spacing holds app-wide.
/// </summary>
public sealed class NasdaqApiClient(HttpClient http, TimeProvider timeProvider) : INasdaqApiClient
{
	private readonly SemaphoreSlim _gate = new(1, 1);
	private DateTimeOffset _lastRequest = DateTimeOffset.MinValue;

	/// <summary>
	/// The HttpClient registered for this client: timeout, gzip, and the browser User-Agent and
	/// Accept Nasdaq wants (without them it doesn't answer).
	/// </summary>
	public static HttpClient CreateHttpClient(HttpMessageHandler? handler = null)
	{
		var client = new HttpClient(
			handler ?? new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All }
		)
		{
			Timeout = NasdaqConstants.HttpTimeout,
		};
		client.DefaultRequestHeaders.TryAddWithoutValidation(
			"User-Agent",
			NasdaqConstants.UserAgent
		);
		client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", NasdaqConstants.Accept);
		return client;
	}

	public async Task<IReadOnlyList<CalendarEventDto>> GetCalendarAsync(
		NasdaqCalendar calendar,
		DateOnly day,
		CancellationToken cancellationToken = default
	)
	{
		var date = CalendarHelpers
			.GetNasdaqRequestDate(calendar, day)
			.ToString(NasdaqConstants.DateFormat, CultureInfo.InvariantCulture);
		var json = await GetStringAsync(
			$"{NasdaqConstants.BaseUrl}{GetPath(calendar)}?date={date}",
			cancellationToken
		);
		return NasdaqCalendarParser.Parse(calendar, json, day);
	}

	private static string GetPath(NasdaqCalendar calendar) =>
		calendar switch
		{
			NasdaqCalendar.Earnings => NasdaqConstants.EarningsPath,
			NasdaqCalendar.Dividends => NasdaqConstants.DividendsPath,
			NasdaqCalendar.Splits => NasdaqConstants.SplitsPath,
			NasdaqCalendar.Economic => NasdaqConstants.EconomicPath,
			_ => throw new ArgumentOutOfRangeException(nameof(calendar), calendar, null),
		};

	private async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken);
		try
		{
			var wait = _lastRequest + NasdaqConstants.RequestSpacing - timeProvider.GetUtcNow();
			if (wait > TimeSpan.Zero)
				await Task.Delay(wait, timeProvider, cancellationToken);
			_lastRequest = timeProvider.GetUtcNow();

			using var response = await http.GetAsync(url, cancellationToken);
			if (!response.IsSuccessStatusCode)
				throw new NasdaqServiceException(
					CalendarValidationCodes.CLVC_008.GetValidationMessage(
						$"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".Trim()
					)
				);
			return await response.Content.ReadAsStringAsync(cancellationToken);
		}
		catch (HttpRequestException ex)
		{
			throw new NasdaqServiceException(
				CalendarValidationCodes.CLVC_008.GetValidationMessage(ex.Message),
				ex
			);
		}
		catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
		{
			throw new NasdaqServiceException(
				CalendarValidationCodes.CLVC_008.GetValidationMessage(
					$"no answer within {(int)NasdaqConstants.HttpTimeout.TotalSeconds} s"
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
