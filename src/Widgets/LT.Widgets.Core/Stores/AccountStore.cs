using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.Contracts.Dtos.Connection;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Stores;

/// <summary>The managed accounts of the current login and the one the UI shows.</summary>
public partial class AccountStore : ObservableObject
{
	private readonly ISettingsManager _settingsManager;

	public AccountStore(IConnectionManager connectionManager, ISettingsManager settingsManager)
	{
		_settingsManager = settingsManager;
		Apply(connectionManager.Status);
		connectionManager.StatusChanged += status => Dispatcher.UIThread.Post(() => Apply(status));
	}

	public ObservableCollection<string> Accounts { get; } = [];

	[ObservableProperty]
	public partial string? SelectedAccount { get; set; }

	/// <summary>
	/// Whose last-known data a widget may show while disconnected: the selected account, or before
	/// the first login the one selected last run. Snapshots are per account, so it is never
	/// another account's data.
	/// </summary>
	public string? SnapshotAccount => SelectedAccount ?? _settingsManager.GetSelectedAccount();

	partial void OnSelectedAccountChanged(string? value)
	{
		if (value != null)
			_settingsManager.SaveSelectedAccount(value);
	}

	private void Apply(ConnectionStatusDto status)
	{
		// Accounts survive a reconnect; only a fresh login can change them
		if (status.State != ConnectionState.Connected || Accounts.SequenceEqual(status.Accounts))
			return;

		Accounts.Clear();
		foreach (var account in status.Accounts)
			Accounts.Add(account);

		var saved = _settingsManager.GetSelectedAccount();
		SelectedAccount =
			saved != null && Accounts.Contains(saved) ? saved : Accounts.FirstOrDefault();
	}
}
