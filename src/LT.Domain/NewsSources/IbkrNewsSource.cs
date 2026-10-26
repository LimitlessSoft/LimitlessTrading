using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.INewsSources;
using LT.Contracts.Requests.News;

namespace LT.Domain.NewsSources;

/// <summary>
/// News of the IB account's API providers (BRFG, DJ-N …). Live headlines are news ticks over
/// reqMktData, so each subscription (one per contract, one per broad tape) counts a market data
/// line. Broad tape contracts are looked up once per provider. Registered as a singleton.
/// </summary>
public class IbkrNewsSource(IIbkrApiClient ibkrApiClient, IMarketDataManager marketDataManager)
	: INewsSource
{
	// Provider code → its broad tape (null: it has none)
	private readonly ConcurrentDictionary<string, ContractDto?> _broadTapes = new();

	public string Id => NewsConstants.IbkrSourceId;

	public string Name => NewsConstants.IbkrSourceName;

	public NewsSourceCapabilities Capabilities =>
		NewsSourceCapabilities.Live
		| NewsSourceCapabilities.History
		| NewsSourceCapabilities.Articles
		| NewsSourceCapabilities.BySymbol
		| NewsSourceCapabilities.BroadTape;

	public bool IsAvailable => ibkrApiClient.IsConnected;

	public async Task<IReadOnlyList<NewsProviderDto>> GetProvidersAsync(
		CancellationToken cancellationToken = default
	)
	{
		var providers = await ibkrApiClient.GetNewsProvidersAsync(cancellationToken);
		foreach (var provider in providers)
			provider.SourceId = Id;
		return providers;
	}

	public async Task<NewsHeadlinesPageDto> GetHeadlinesAsync(
		GetNewsHeadlinesRequest request,
		CancellationToken cancellationToken = default
	)
	{
		var codes = NewsHelpers.GetProviderCodes(request.Providers, Id);
		if (codes.Count == 0)
			return new NewsHeadlinesPageDto();

		var page = await ibkrApiClient.GetHistoricalNewsAsync(
			request.Contract.ConId,
			codes,
			Math.Min(request.Limit, NewsConstants.MaxHeadlinesPerRequest),
			request.Before,
			cancellationToken
		);
		var symbol = ContractHelpers.GetDisplayName(request.Contract);
		foreach (var headline in page.Headlines)
		{
			headline.SourceId = Id;
			headline.ContractId = request.Contract.ConId;
			headline.Symbol = symbol;
		}
		return page;
	}

	public async IAsyncEnumerable<NewsHeadlineDto> SubscribeHeadlinesAsync(
		SubscribeNewsRequest request,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var codes = NewsHelpers.GetProviderCodes(request.Providers, Id);
		if (codes.Count == 0)
			yield break;

		List<IAsyncEnumerable<NewsHeadlineDto>> streams = [];
		if (request.Contract is { } contract)
			streams.Add(ibkrApiClient.SubscribeNewsAsync(contract, codes, cancellationToken));
		else
			foreach (var code in codes)
				if (
					await NewsHelpers.ResolveBroadTapeAsync(
						_broadTapes,
						code,
						ibkrApiClient.GetBroadTapeContractsAsync,
						cancellationToken
					) is
					{ } tape
				)
					streams.Add(ibkrApiClient.SubscribeNewsAsync(tape, null, cancellationToken));
		if (streams.Count == 0)
			throw new LSCoreBadRequestException(NewsValidationCodes.NVC_003.GetValidationMessage());

		var lines = 0;
		try
		{
			for (; lines < streams.Count; lines++)
				marketDataManager.AcquireLine();

			var symbol = request.Contract is { } linked
				? ContractHelpers.GetDisplayName(linked)
				: string.Empty;
			await foreach (
				var headline in AsyncEnumerableHelpers.MergeAsync(streams, cancellationToken)
			)
			{
				headline.SourceId = Id;
				headline.ContractId = request.Contract?.ConId ?? 0;
				headline.Symbol = symbol;
				yield return headline;
			}
		}
		finally
		{
			for (; lines > 0; lines--)
				marketDataManager.ReleaseLine();
		}
	}

	public async Task<bool> HasBroadTapeAsync(
		string providerCode,
		CancellationToken cancellationToken = default
	) =>
		await NewsHelpers.ResolveBroadTapeAsync(
			_broadTapes,
			providerCode,
			ibkrApiClient.GetBroadTapeContractsAsync,
			cancellationToken
		) != null;

	public Task<NewsArticleDto> GetArticleAsync(
		NewsHeadlineDto headline,
		CancellationToken cancellationToken = default
	) =>
		ibkrApiClient.GetNewsArticleAsync(
			headline.ProviderCode,
			headline.ArticleId,
			cancellationToken
		);
}
