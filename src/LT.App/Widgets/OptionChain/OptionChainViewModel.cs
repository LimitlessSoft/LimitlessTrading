using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
		UpdateUnderlying();

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

	public ObservableCollection<OptionRowViewModel> Rows { get; } = [];
	public ObservableCollection<string> Expirations { get; } = [];

	[ObservableProperty]
	public partial string Expiry { get; set; } = DateTime.Today.ToString("yyyyMM");

	[ObservableProperty]
	public partial string? SelectedExpiration { get; set; }

	[ObservableProperty]
	public partial string UnderlyingText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool CanLoad { get; set; }

	[ObservableProperty]
	public partial bool IsLoading { get; set; }

	[ObservableProperty]
	public partial string? InfoText { get; set; }

	partial void OnSelectedExpirationChanged(string? value) => ShowExpiration();

	protected override void OnConnected(CancellationToken session)
	{
		_session = session;
		UpdateUnderlying();
		if (_chain != null)
			ShowExpiration();
	}

	protected override void OnDisconnected()
	{
		StopQuotes();
		UpdateUnderlying();
	}

	[RelayCommand]
	private async Task LoadAsync()
	{
		if (_underlying is not { } underlying)
			return;

		ErrorMessage = null;
		IsLoading = true;
		try
		{
			_chain = await _optionChainManager.GetChainAsync(
				new GetOptionChainRequest { Underlying = underlying, Expiry = Expiry.Trim() },
				_session
			);
			Expirations.Clear();
			foreach (var expiration in _chain.Expirations)
				Expirations.Add(expiration);
			SelectedExpiration = Expirations.FirstOrDefault();
			ShowExpiration();
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
		finally
		{
			IsLoading = false;
		}
	}

	public override void Dispose()
	{
		Link.Dispose();
		StopQuotes();
		base.Dispose();
	}

	private void OnContractReceived(ContractDto contract)
	{
		if (contract.ConId == _underlying?.ConId)
			return;

		_underlying = contract;
		StopQuotes();
		_chain = null;
		Rows.Clear();
		Expirations.Clear();
		InfoText = null;
		UpdateUnderlying();
	}

	private void UpdateUnderlying()
	{
		var contract = _underlying;
		CanLoad = IsConnected && contract != null && ContractHelpers.SupportsOptionChain(contract);
		UnderlyingText =
			contract == null ? "Select a stock or index"
			: ContractHelpers.SupportsOptionChain(contract)
				? ContractHelpers.GetDisplayName(contract)
			: $"{ContractHelpers.GetDisplayName(contract)} has no option chain";
	}

	private void ShowExpiration()
	{
		StopQuotes();
		Rows.Clear();
		if (_chain == null || SelectedExpiration == null)
			return;

		var strikes = _chain.Strikes.Where(x => x.Expiration == SelectedExpiration).ToList();
		var visible = OptionChainHelpers.AroundPrice(
			strikes,
			_chain.UnderlyingPrice,
			MarketDataConstants.OptionStrikesAroundAtm
		);
		var atm = OptionChainHelpers
			.AroundPrice(visible, _chain.UnderlyingPrice, 0)
			.FirstOrDefault();
		foreach (var strike in visible)
			Rows.Add(new OptionRowViewModel(strike, strike == atm));

		InfoText =
			$"Underlying {FormatHelpers.Price(_chain.UnderlyingPrice)} · showing {visible.Count} of {strikes.Count} strikes around the money";

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
