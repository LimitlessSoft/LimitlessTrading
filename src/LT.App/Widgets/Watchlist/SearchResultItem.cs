using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Watchlist;

public record SearchResultItem(ContractDetailsDto Details)
{
	public override string ToString()
	{
		var contract = Details.Contract;
		var name = string.IsNullOrEmpty(Details.LongName) ? string.Empty : $" · {Details.LongName}";
		return $"{ContractHelpers.GetDisplayName(contract)}{name} · {ContractHelpers.GetListingExchange(contract)} · {contract.Currency}";
	}
}
