using CommunityToolkit.Mvvm.ComponentModel;
using LT.Contracts.Dtos.Contracts;

namespace LT.App.Stores;

/// <summary>The contract the chart and option chain follow (picked in watchlist or positions).</summary>
public partial class SelectedContractStore : ObservableObject
{
	[ObservableProperty]
	public partial ContractDto? SelectedContract { get; set; }
}
