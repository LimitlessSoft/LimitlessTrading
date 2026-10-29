using System.Globalization;
using System.Net;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Dtos.Fundamentals;
using LT.Contracts.Interfaces.IApiClients;

namespace LT.Sec;

/// <summary>
/// SEC EDGAR over HTTPS: the ticker → CIK file, XBRL share counts, financial statement facts,
/// filings' cover pages and the latest filings feed. Every request goes through
/// one gate and stays <see cref="SecConstants.RequestSpacing"/> after the previous one (the SEC
/// allows 10 per second per client). Singleton (Program.cs) so the spacing holds app-wide.
/// </summary>
public sealed class SecApiClient(HttpClient http, TimeProvider timeProvider) : ISecApiClient
{
	private readonly SemaphoreSlim _gate = new(1, 1);
	private DateTimeOffset _lastRequest = DateTimeOffset.MinValue;

	/// <summary>
	/// The HttpClient registered for this client: timeout, gzip, and the User-Agent with a contact
	/// address the SEC requires (it answers 403 without one).
	/// </summary>
	public static HttpClient CreateHttpClient(HttpMessageHandler? handler = null)
	{
		var client = new HttpClient(
			handler ?? new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All }
		)
		{
			Timeout = SecConstants.HttpTimeout,
		};
		var version = typeof(SecApiClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
		client.DefaultRequestHeaders.TryAddWithoutValidation(
			"User-Agent",
			$"{SecConstants.UserAgentProduct}/{version} {SecConstants.UserAgentContact}"
		);
		return client;
	}

	public async Task<IReadOnlyDictionary<string, int>> GetTickerCiksAsync(
		CancellationToken cancellationToken = default
	) =>
		SecJsonParser.ParseCompanyTickers(
			await GetStringAsync(SecConstants.CompanyTickersUrl, cancellationToken)
				?? throw new HttpRequestException(
					"SEC company tickers not found.",
					null,
					HttpStatusCode.NotFound
				)
		);

	public async Task<IReadOnlyList<SecFactDto>?> GetSharesOutstandingFactsAsync(
		int cik,
		CancellationToken cancellationToken = default
	) =>
		await GetStringAsync(
			GetUrl(SecConstants.SharesOutstandingConceptUrl, cik),
			cancellationToken
		)
			is { } json
			? SecJsonParser.ParseConcept(json)
			: null;

	public async Task<IReadOnlyList<SecFactDto>?> GetCompanyShareFactsAsync(
		int cik,
		CancellationToken cancellationToken = default
	) =>
		await GetStringAsync(GetUrl(SecConstants.CompanyFactsUrl, cik), cancellationToken)
			is { } json
			? SecJsonParser.ParseCompanyShareFacts(json)
			: null;

	public async Task<SecCompanyFactsDto?> GetCompanyFinancialFactsAsync(
		int cik,
		IReadOnlyCollection<string> concepts,
		CancellationToken cancellationToken = default
	) =>
		await GetStringAsync(GetUrl(SecConstants.CompanyFactsUrl, cik), cancellationToken)
			is { } json
			? SecJsonParser.ParseCompanyFinancialFacts(json, concepts)
			: null;

	public async Task<IReadOnlyList<SecFactDto>?> GetLatestFilingShareFactsAsync(
		int cik,
		CancellationToken cancellationToken = default
	)
	{
		var submissions = await GetStringAsync(
			GetUrl(SecConstants.SubmissionsUrl, cik),
			cancellationToken
		);
		if (
			submissions == null
			|| SecJsonParser.ParseLatestInlineFiling(
				submissions,
				SecConstants.SharesOutstandingForms
			)
				is not { } filing
		)
			return null;

		var url = string.Format(
			CultureInfo.InvariantCulture,
			SecConstants.FilingInstanceUrl,
			cik,
			filing.AccessionNumber.Replace("-", string.Empty),
			Path.GetFileNameWithoutExtension(filing.PrimaryDocument)
		);
		return await GetStringAsync(url, cancellationToken) is { } xml
			? SecXbrlParser.ParseCoverPageShares(xml, filing.Form, filing.Filed)
			: null;
	}

	public async Task<IReadOnlyList<SecCurrentFilingDto>> GetCurrentFilingsAsync(
		string form,
		int start,
		CancellationToken cancellationToken = default
	)
	{
		var url = string.Format(
			CultureInfo.InvariantCulture,
			SecConstants.CurrentFilingsUrl,
			Uri.EscapeDataString(form),
			start
		);
		return await GetStringAsync(url, cancellationToken) is { } xml
			? SecAtomParser.ParseCurrentFilings(xml)
			: [];
	}

	private static string GetUrl(string format, int cik) =>
		string.Format(
			CultureInfo.InvariantCulture,
			format,
			cik.ToString("D10", CultureInfo.InvariantCulture)
		);

	// The body, or null on 404 (the SEC has nothing for the company / concept)
	private async Task<string?> GetStringAsync(string url, CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken);
		try
		{
			var wait = _lastRequest + SecConstants.RequestSpacing - timeProvider.GetUtcNow();
			if (wait > TimeSpan.Zero)
				await Task.Delay(wait, timeProvider, cancellationToken);
			_lastRequest = timeProvider.GetUtcNow();

			using var response = await http.GetAsync(url, cancellationToken);
			if (response.StatusCode == HttpStatusCode.NotFound)
				return null;
			response.EnsureSuccessStatusCode();
			return await response.Content.ReadAsStringAsync(cancellationToken);
		}
		finally
		{
			_gate.Release();
		}
	}
}
