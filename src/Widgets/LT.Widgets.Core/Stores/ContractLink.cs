using CommunityToolkit.Mvvm.ComponentModel;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;

namespace LT.App.Stores;

/// <summary>
/// One widget's membership in a link group. The widget publishes the contract it switches to and
/// gets the group's contract through <see cref="ContractReceived"/>, never its own back.
/// </summary>
public partial class ContractLink : ObservableObject, IDisposable
{
	private readonly LinkGroupStore _store;
	private readonly Func<ContractDto?> _current;
	private bool _publishing;

	/// <param name="current">The widget's contract, published when it joins an empty group.</param>
	public ContractLink(LinkGroupStore store, Func<ContractDto?> current)
	{
		_store = store;
		_current = current;
		store.ContractChanged += OnContractChanged;
	}

	/// <summary>New widgets start in the first group, so they follow each other out of the box.</summary>
	[ObservableProperty]
	public partial LinkGroup Group { get; set; } = LinkGroup.Red;

	public event Action<ContractDto>? ContractReceived;

	public void Publish(ContractDto contract)
	{
		_publishing = true;
		try
		{
			_store.Set(Group, contract);
		}
		finally
		{
			_publishing = false;
		}
	}

	public void Dispose()
	{
		_store.ContractChanged -= OnContractChanged;
		GC.SuppressFinalize(this);
	}

	// Joining a group shows its contract; an empty group takes this widget's
	partial void OnGroupChanged(LinkGroup value)
	{
		if (_store.Get(value) is { } contract)
			ContractReceived?.Invoke(contract);
		else if (_current() is { } current)
			Publish(current);
	}

	private void OnContractChanged(LinkGroup group, ContractDto contract)
	{
		if (group == Group && !_publishing)
			ContractReceived?.Invoke(contract);
	}
}
