using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Contracts;

namespace LT.App.Widgets.Watchlist;

public partial class WatchlistViewModel : ConnectedViewModelBase, ILinkable
{
	private readonly IWatchlistManager _watchlistManager;
	private readonly IContractManager _contractManager;
	private readonly IMarketDataManager _marketDataManager;
	private readonly IMessageLogManager _messageLogManager;
	private CancellationToken _session = new(true);

	public WatchlistViewModel(
		IWatchlistManager watchlistManager,
		IContractManager contractManager,
		IMarketDataManager marketDataManager,
		IConnectionManager connectionManager,
		IMessageLogManager messageLogManager,
		ISettingsManager settingsManager,
		LinkGroupStore linkGroupStore
	)
		: base(connectionManager)
	{
		_watchlistManager = watchlistManager;
		_contractManager = contractManager;
		_marketDataManager = marketDataManager;
		_messageLogManager = messageLogManager;
		Link = new ContractLink(linkGroupStore, () => SelectedRow?.Contract);

		foreach (var contract in watchlistManager.GetItems())
			Rows.Add(new WatchlistRowViewModel(contract));
		SelectedRow = Rows.FirstOrDefault();

		settingsManager.AppSettingsChanged += _ => Dispatcher.UIThread.Post(RestartSession);

		new DispatcherTimer(
			MarketDataConstants.UiFlushInterval,
			DispatcherPriority.Background,
			(_, _) =>
			{
				foreach (var row in Rows)
					row.Flush();
			}
		).Start();
	}

	public ObservableCollection<WatchlistRowViewModel> Rows { get; } = [];

	/// <summary>Clicking a row sends its contract to the link group.</summary>
	public ContractLink Link { get; }

	public ObservableCollection<SearchResultItem> SearchResults { get; } = [];
	public IReadOnlyList<string> SecurityTypes => MarketDataConstants.SearchableSecurityTypes;

	[ObservableProperty]
	public partial WatchlistRowViewModel? SelectedRow { get; set; }

	[ObservableProperty]
	public partial string SearchText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string SecurityType { get; set; } = MarketDataConstants.StockType;

	[ObservableProperty]
	public partial bool IsSearching { get; set; }

	[ObservableProperty]
	public partial bool HasSearchResults { get; set; }

	partial void OnSelectedRowChanged(WatchlistRowViewModel? value)
	{
		if (value != null)
			Link.Publish(value.Contract);
	}

	protected override void OnConnected(CancellationToken session)
	{
		_session = session;
		foreach (var row in Rows)
			Start(row);
	}

	protected override void OnDisconnected()
	{
		foreach (var row in Rows)
			Stop(row);
	}

	public override void Dispose()
	{
		Link.Dispose();
		base.Dispose();
	}

	[RelayCommand]
	private async Task SearchAsync()
	{
		ErrorMessage = null;
		SearchResults.Clear();
		HasSearchResults = false;
		if (string.IsNullOrWhiteSpace(SearchText))
			return;

		IsSearching = true;
		try
		{
			var results = await _contractManager.SearchAsync(
				new ContractSearchRequest { Symbol = SearchText, SecurityType = SecurityType },
				_session
			);
			foreach (var result in results.Take(50))
				SearchResults.Add(new SearchResultItem(result));
			HasSearchResults = SearchResults.Count > 0;
			if (!HasSearchResults)
				ErrorMessage = $"No {SecurityType} contracts found for '{SearchText.Trim()}'.";
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
		finally
		{
			IsSearching = false;
		}
	}

	[RelayCommand]
	private void Add(SearchResultItem? item)
	{
		if (item == null)
			return;

		try
		{
			_watchlistManager.Add(item.Details.Contract);
			var row = new WatchlistRowViewModel(item.Details.Contract);
			Rows.Add(row);
			SelectedRow = row;
			if (IsConnected)
				Start(row);
			SearchResults.Clear();
			HasSearchResults = false;
			SearchText = string.Empty;
			ErrorMessage = null;
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	[RelayCommand]
	private void CloseSearch()
	{
		SearchResults.Clear();
		HasSearchResults = false;
	}

	[RelayCommand]
	private void Remove(WatchlistRowViewModel? row)
	{
		row ??= SelectedRow;
		if (row == null)
			return;

		Stop(row);
		_watchlistManager.Remove(row.Contract.ConId);
		Rows.Remove(row);
	}

	private void Start(WatchlistRowViewModel row)
	{
		Stop(row);
		var subscription = CancellationTokenSource.CreateLinkedTokenSource(_session);
		row.Subscription = subscription;
		var token = subscription.Token;
		_ = Task.Run(
			async () =>
			{
				try
				{
					await foreach (
						var quote in _marketDataManager
							.SubscribeQuotesAsync(row.Contract, token)
							.ConfigureAwait(false)
					)
						row.Pending.Set(quote);
				}
				catch (OperationCanceledException) { }
				catch (Exception ex)
				{
					var message = ErrorMessageHelpers.ToUserMessage(ex);
					Dispatcher.UIThread.Post(() => row.Error = message);
					_messageLogManager.Add(
						MessageSeverity.Warning,
						MessageLogConstants.SourceMarketData,
						$"{row.Symbol}: {message}"
					);
				}
			},
			token
		);
	}

	private static void Stop(WatchlistRowViewModel row)
	{
		row.Subscription?.Cancel();
		row.Subscription?.Dispose();
		row.Subscription = null;
	}
}
