using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Options;

namespace LT.App.Widgets.OptionChain;

/// <summary>
/// Calls and puts per strike for one expiration. Quotes are streamed only for the strikes around
/// the underlying price, to stay inside the market data line limit.
/// </summary>
public partial class OptionChainViewModel : ConnectedViewModelBase, ILinkable
{
	private readonly IOptionChainManager _optionChainManager;
	private readonly IMarketDataManager _marketDataManager;
	private readonly IMessageLogManager _messageLogManager;
	private CancellationToken _session = new(true);
	private ContractDto? _underlying;
	private CancellationTokenSource? _load;
	private CancellationTokenSource? _quotes;
	private OptionChainDto? _chain;

	public OptionChainViewModel(
		IOptionChainManager optionChainManager,
		IMarketDataManager marketDataManager,
		IConnectionManager connectionManager,
		IMessageLogManager messageLogManager,
		LinkGroupStore linkGroupStore
	)
		: base(connectionManager)
	{
		_optionChainManager = optionChainManager;
		_marketDataManager = marketDataManager;
		_messageLogManager = messageLogManager;
		Link = new ContractLink(linkGroupStore, () => _underlying);
		Link.ContractReceived += OnContractReceived;
		_underlying = linkGroupStore.Get(Link.Group);
		_ = LoadExpirationsAsync();

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

	/// <summary>The underlying comes from the link group (watchlist, positions, a chart).</summary>
	public ContractLink Link { get; }

	public ObservableCollection<ExpirationTab> Expirations { get; } = [];
	public ObservableCollection<OptionRowViewModel> Rows { get; } = [];

	/// <summary>Frame header title: the underlying's ticker.</summary>
	[ObservableProperty]
	public partial string Title { get; set; } = "Option chain";

	[ObservableProperty]
	public partial ExpirationTab? SelectedExpiration { get; set; }

	[ObservableProperty]
	public partial bool IsLoading { get; set; }

	[ObservableProperty]
	public partial string? InfoText { get; set; }

	partial void OnSelectedExpirationChanged(ExpirationTab? value)
	{
		if (value != null)
			_ = LoadChainAsync(value);
	}

	protected override void OnConnected(CancellationToken session)
	{
		_session = session;
		_ = LoadExpirationsAsync();
	}

	protected override void OnDisconnected()
	{
		CancelLoad();
		StopQuotes();
	}

	public override void Dispose()
	{
		Link.Dispose();
		CancelLoad();
		StopQuotes();
		base.Dispose();
	}

	private void OnContractReceived(ContractDto contract)
	{
		if (contract.ConId == _underlying?.ConId)
			return;
		_underlying = contract;
		_ = LoadExpirationsAsync();
	}

	// New underlying or connection: list its expirations and open the nearest
	private async Task LoadExpirationsAsync()
	{
		var load = RestartLoad();
		StopQuotes();
		_chain = null;
		Rows.Clear();
		SelectedExpiration = null;
		Expirations.Clear();
		ErrorMessage = null;

		var underlying = _underlying;
		Title = underlying == null ? "Option chain" : ContractHelpers.GetDisplayName(underlying);
		if (underlying == null)
		{
			InfoText = "Pick a stock or index in a widget of the same link group.";
			return;
		}
		if (!ContractHelpers.SupportsOptionChain(underlying))
		{
			InfoText = $"{ContractHelpers.GetDisplayName(underlying)} has no option chain.";
			return;
		}
		InfoText = null;
		if (!IsConnected)
			return;

		IsLoading = true;
		try
		{
			var expirations = await _optionChainManager.GetExpirationsAsync(underlying, load);
			var today = DateTime.Today;
			foreach (var expiration in expirations)
				Expirations.Add(new ExpirationTab(expiration, today));
			SelectedExpiration = Expirations.FirstOrDefault();
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
		finally
		{
			if (!load.IsCancellationRequested)
				IsLoading = false;
		}
	}

	private async Task LoadChainAsync(ExpirationTab tab)
	{
		if (_underlying is not { } underlying)
			return;

		var load = RestartLoad();
		StopQuotes();
		_chain = null;
		Rows.Clear();
		ErrorMessage = null;
		InfoText = null;
		IsLoading = true;
		try
		{
			_chain = await _optionChainManager.GetChainAsync(
				new GetOptionChainRequest
				{
					Underlying = underlying,
					Expiry = tab.Expiration.Expiration,
					TradingClass = tab.Expiration.TradingClass,
				},
				load
			);
			ShowChain();
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
		finally
		{
			if (!load.IsCancellationRequested)
				IsLoading = false;
		}
	}

	private void ShowChain()
	{
		if (_chain == null)
			return;

		var visible = OptionChainHelpers.AroundPrice(
			_chain.Strikes,
			_chain.UnderlyingPrice,
			MarketDataConstants.OptionStrikesAroundAtm
		);
		var atm = OptionChainHelpers
			.AroundPrice(visible, _chain.UnderlyingPrice, 0)
			.FirstOrDefault();
		foreach (var strike in visible)
			Rows.Add(new OptionRowViewModel(strike, strike == atm));

		InfoText =
			$"Underlying {FormatHelpers.Price(_chain.UnderlyingPrice)} · {visible.Count} of {_chain.Strikes.Count} strikes around the money";

		if (!IsConnected)
			return;

		_quotes = CancellationTokenSource.CreateLinkedTokenSource(_session);
		foreach (var row in Rows)
		{
			if (row.Strike.Call is { } call)
				StartQuote(call, row.CallPending, _quotes.Token);
			if (row.Strike.Put is { } put)
				StartQuote(put, row.PutPending, _quotes.Token);
		}
	}

	// One load at a time: a newer underlying or tab cancels the one in flight
	private CancellationToken RestartLoad()
	{
		CancelLoad();
		_load = CancellationTokenSource.CreateLinkedTokenSource(_session);
		return _load.Token;
	}

	private void CancelLoad()
	{
		_load?.Cancel();
		_load?.Dispose();
		_load = null;
	}

	private void StartQuote(
		ContractDto contract,
		LatestValue<QuoteDto> target,
		CancellationToken token
	) =>
		_ = Task.Run(
			async () =>
			{
				try
				{
					await foreach (
						var quote in _marketDataManager
							.SubscribeQuotesAsync(contract, token)
							.ConfigureAwait(false)
					)
						target.Set(quote);
				}
				catch (OperationCanceledException) { }
				catch (Exception ex)
				{
					_messageLogManager.Add(
						MessageSeverity.Warning,
						MessageLogConstants.SourceOptions,
						$"{ContractHelpers.GetDisplayName(contract)}: {ErrorMessageHelpers.ToUserMessage(ex)}"
					);
				}
			},
			token
		);

	private void StopQuotes()
	{
		_quotes?.Cancel();
		_quotes?.Dispose();
		_quotes = null;
	}
}
