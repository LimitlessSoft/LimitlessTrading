using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

/// <summary>A recently used contract offered in the chart's ticker box.</summary>
public class RecentContractItem(ContractDto contract)
{
	public ContractDto Contract => contract;
	public string Symbol => contract.Symbol;
	public string Description => ContractHelpers.GetDisplayName(contract);

	// AutoCompleteBox puts this text in the box when an item is picked
	public override string ToString() => Symbol;
}
