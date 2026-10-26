using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Controls;

/// <summary>
/// A contract offered in a <see cref="ContractSearchBox"/> (recent, or an IB search result). The description
/// tells listings of the same symbol apart, e.g. SPY on ARCA in USD vs SPY on ASX in AUD.
/// </summary>
public class ContractSuggestionItem(ContractDto contract, string? name = null)
{
	public ContractDto Contract => contract;
	public string Symbol => contract.Symbol;
	public string Description =>
		string.Join(
			" · ",
			new[]
			{
				ContractHelpers.GetDisplayName(contract) is var display
				&& display != contract.Symbol
					? display
					: null,
				name,
				ContractHelpers.GetListingExchange(contract),
				contract.Currency,
			}.Where(x => !string.IsNullOrEmpty(x))
		);

	// AutoCompleteBox puts this text in the box when an item is picked
	public override string ToString() => Symbol;

	/// <summary>
	/// Suggestions for typed text: every stock / ETF, index and crypto listing of the symbol. Errors
	/// (e.g. half-typed symbols) leave the list empty; Enter still resolves the ticker.
	/// </summary>
	public static async Task<IEnumerable<object>> SearchAsync(
		IContractManager contractManager,
		string text,
		CancellationToken cancellationToken
	)
	{
		try
		{
			var results = await contractManager.SearchSymbolAsync(text, cancellationToken);
			return results
				.Take(MarketDataConstants.TickerSuggestionsMax)
				.Select(x => new ContractSuggestionItem(x.Contract, x.LongName))
				.ToList();
		}
		catch (Exception)
		{
			return [];
		}
	}
}
