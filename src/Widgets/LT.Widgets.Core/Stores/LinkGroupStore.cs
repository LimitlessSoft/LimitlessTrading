using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;

namespace LT.App.Stores;

/// <summary>The contract of each link group; used on the UI thread only (see ContractLink).</summary>
public class LinkGroupStore
{
	private readonly Dictionary<LinkGroup, ContractDto> _contracts = [];

	/// <summary>A group got a new contract.</summary>
	public event Action<LinkGroup, ContractDto>? ContractChanged;

	public ContractDto? Get(LinkGroup group) => _contracts.GetValueOrDefault(group);

	public void Set(LinkGroup group, ContractDto contract)
	{
		if (group == LinkGroup.None || Get(group)?.ConId is { } conId && conId == contract.ConId)
			return;
		_contracts[group] = contract;
		ContractChanged?.Invoke(group, contract);
	}
}
