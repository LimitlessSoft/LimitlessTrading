using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

/// <summary>
/// A contract offered in the chart's ticker box (recent, or an IB search result). The description
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
}
