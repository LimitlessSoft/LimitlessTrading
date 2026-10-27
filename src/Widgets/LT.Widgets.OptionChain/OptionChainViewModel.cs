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
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Options;
using LT.Contracts.Requests.Orders;

namespace LT.App.Widgets.OptionChain;

/// <summary>
/// Calls and puts per strike for one expiration. Quotes are streamed only for the strikes around
/// the underlying price, to stay inside the market data line limit. Bid / ask cells trade the
/// widget's one-click quantity at market with a single click (no confirmation; the user asked for
/// it). The underlying comes from the link group, or is typed into the header title.
/// </summary>
public partial class OptionChainViewModel
	: ConnectedViewModelBase,
		ILinkable,
		ITickerTitled,
		IWidgetState
{
	private readonly IOptionChainManager _optionChainManager;
	private readonly IContractManager _contractManager;
	private readonly IMarketDataManager _marketDataManager;
	private readonly IMessageLogManager _messageLogManager;
	private readonly IOrderManager _orderManager;
	private readonly AccountStore _accountStore;
	private readonly TimeProvider _timeProvider;
	private readonly DisplayTimeStore _displayTime;
	private readonly DispatcherTimer _tradeStatusTimer;

	// Double-send guard for one-click orders, per cell (contract + side)
	private readonly Dictionary<string, DateTimeOffset> _lastQuickOrderClicks = [];
	private readonly HashSet<string> _quickOrdersInFlight = [];
	private CancellationToken _session = new(true);
	private ContractDto? _underlying;
	private CancellationTokenSource? _load;
	private CancellationTokenSource? _quotes;
	private OptionChainDto? _chain;
	private double? _underlyingPrice;
	private double? _impliedVolatility;
	private DateTimeOffset? _expiration;

	// Index in _chain.Strikes the visible window was built around, and the quote streams of each
	// visible row (one cancellation per row, so rows leaving the window can be unsubscribed)
	private int _center;
	private readonly Dictionary<OptionRowViewModel, RowQuotes> _rowQuotes = [];
	private bool _recentering;

	// Replaced per chain, so a stream cancelled with the previous chain can't write into this one
	private LatestValue<QuoteDto> _underlyingPending = new();

	public OptionChainViewModel(
		IOptionChainManager optionChainManager,
		IContractManager contractManager,
		IMarketDataManager marketDataManager,
		IConnectionManager connectionManager,
		IMessageLogManager messageLogManager,
		IOrderManager orderManager,
		LinkGroupStore linkGroupStore,
		AccountStore accountStore,
		TimeProvider timeProvider,
		DisplayTimeStore displayTime
	)
		: base(connectionManager)
	{
		_displayTime = displayTime;
		_optionChainManager = optionChainManager;
		_contractManager = contractManager;
		_marketDataManager = marketDataManager;
		_messageLogManager = messageLogManager;
		_orderManager = orderManager;
		_accountStore = accountStore;
		_timeProvider = timeProvider;
		_tradeStatusTimer = new DispatcherTimer
		{
			Interval = OrderConstants.QuickOrderStatusDuration,
		};
		_tradeStatusTimer.Tick += (_, _) =>
		{
			_tradeStatusTimer.Stop();
			TradeStatus = null;
		};
		Link = new ContractLink(linkGroupStore, () => _underlying);
		Link.ContractReceived += OnContractReceived;
		TickerEditor = new TickerTitleEditor(
			contractManager,
			() => IsConnected,
			OpenTickerAsync,
			OpenContract
		);
		_underlying = linkGroupStore.Get(Link.Group);
		_ = LoadExpirationsAsync();

		new DispatcherTimer(
			MarketDataConstants.UiFlushInterval,
			DispatcherPriority.Background,
			(_, _) =>
			{
				FlushUnderlyingPrice();
				foreach (var row in Rows)
					row.Flush();
			}
		).Start();
	}

	/// <summary>
	/// Raised on the UI thread after the strike window re-centred, with the new ATM row, so the
	/// view can keep it in sight.
	/// </summary>
	public event Action<OptionRowViewModel>? Recentered;

	/// <summary>The underlying comes from the link group (watchlist, positions, a chart).</summary>
	public ContractLink Link { get; }

	/// <summary>The header title's ticker box: typing an underlying there opens its chain.</summary>
	public TickerTitleEditor TickerEditor { get; }

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

	/// <summary>The last one-click order sent, shown for a few seconds.</summary>
	[ObservableProperty]
	public partial string? TradeStatus { get; set; }

	/// <summary>
	/// Contracts per one-click order, saved with the widget. Null while the box is empty: a click
	/// then sends nothing and says why.
	/// </summary>
	[ObservableProperty]
	public partial decimal? QuickOrderQuantity { get; set; } =
		OrderConstants.QuickOrderQuantityDefault;

	public void LoadState(WidgetPlacementDto placement) =>
		QuickOrderQuantity = OptionChainHelpers.GetQuickOrderQuantityOrDefault(
			placement.OptionQuickOrderQuantity
		);

	// An empty or invalid box saves nothing, so the next start has the default
	public void SaveState(WidgetPlacementDto placement) =>
		placement.OptionQuickOrderQuantity = OptionChainHelpers.IsValidQuickOrderQuantity(
			QuickOrderQuantity
		)
			? (int)QuickOrderQuantity!.Value
			: null;

	partial void OnQuickOrderQuantityChanged(decimal? value)
	{
		foreach (var row in Rows)
			row.SetQuickOrderQuantity(value);
	}

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
		foreach (var row in Rows)
			row.RefreshTrading();
	}

	public override void Dispose()
	{
		Link.Dispose();
		CancelLoad();
		StopQuotes();
		base.Dispose();
	}

	/// <summary>Ticker box: resolves the ticker and shows its chain.</summary>
	public async Task OpenTickerAsync(string ticker)
	{
		ErrorMessage = null;
		try
		{
			ShowUnderlying(await _contractManager.ResolveTickerAsync(ticker));
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	/// <summary>Ticker box: a recent or suggested listing, already resolved.</summary>
	public void OpenContract(ContractDto contract)
	{
		_contractManager.AddRecentContract(contract);
		ShowUnderlying(contract);
	}

	// A ticker typed here moves every widget in this chain's link group along, like a chart's
	private void ShowUnderlying(ContractDto contract)
	{
		OnContractReceived(contract);
		Link.Publish(contract);
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
			// Days to expiry count from today in the display zone
			var today = _displayTime.Now.Date;
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
		_impliedVolatility = null;
		_expiration = OptionChainHelpers.GetExpirationTime(tab.Expiration.Expiration);
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

		// Unknown price: centred on the middle strike until the underlying's stream brings one
		_center =
			OptionChainHelpers.GetNearestStrikeIndex(_chain.Strikes, _chain.UnderlyingPrice)
			?? _chain.Strikes.Count / 2;
		var (from, to) = OptionChainHelpers.GetWindow(
			_chain.Strikes.Count,
			_center,
			MarketDataConstants.OptionStrikesAroundAtm
		);
		for (var i = from; i <= to; i++)
			Rows.Add(CreateRow(_chain.Strikes[i]));
		ApplyUnderlyingPrice(_chain.UnderlyingPrice);

		if (!IsConnected)
			return;

		// The underlying keeps streaming so the moneyness shading follows its price
		_quotes = CancellationTokenSource.CreateLinkedTokenSource(_session);
		_underlyingPending = new LatestValue<QuoteDto>();
		// … and carries its option implied volatility (tick 106) for the σ bands, on the same line
		StartQuote(
			_chain.Underlying,
			_underlyingPending,
			_quotes.Token,
			MarketDataConstants.OptionImpliedVolatilityGenericTick
		);
		foreach (var row in Rows)
			StartRowQuotes(row, _quotes.Token);
	}

	private OptionRowViewModel CreateRow(OptionStrikeDto strike)
	{
		var row = new OptionRowViewModel(strike, QuickTradeAsync, () => IsConnected);
		row.SetQuickOrderQuantity(QuickOrderQuantity);
		return row;
	}

	/// <summary>
	/// One click on a bid / ask cell: <see cref="QuickOrderQuantity"/> contracts at market, DAY, on
	/// the selected account; an empty or out-of-range quantity sends nothing and shows why. Repeat
	/// clicks on the same cell are dropped while its order is in flight and for a short window
	/// after, so a double click sends one order.
	/// </summary>
	public async Task QuickTradeAsync(
		OptionRowViewModel row,
		string right,
		OptionQuoteColumn column
	)
	{
		if (!IsConnected)
			return;
		var quantity = QuickOrderQuantity;
		if (OptionChainHelpers.GetQuickOrderQuantityError(quantity) is { } quantityError)
		{
			ErrorMessage = $"Order not sent: {quantityError}";
			return;
		}
		var request = OptionChainHelpers.BuildQuickOrder(
			row.Strike,
			right,
			column,
			_accountStore.SelectedAccount,
			quantity
		);
		if (request == null)
			return;

		var key = OptionChainHelpers.GetQuickOrderKey(request);
		var now = _timeProvider.GetUtcNow();
		DateTimeOffset? lastClick = _lastQuickOrderClicks.TryGetValue(key, out var last)
			? last
			: null;
		if (_quickOrdersInFlight.Contains(key) || OptionChainHelpers.IsRepeatClick(lastClick, now))
			return;
		_lastQuickOrderClicks[key] = now;
		_quickOrdersInFlight.Add(key);

		var description = OrderHelpers.Describe(request);
		ErrorMessage = null;
		try
		{
			var placed = await _orderManager.PlaceOrderAsync(request, _session);
			ShowTradeStatus($"Order {placed.OrderId} sent: {description}");
			Log(MessageSeverity.Info, $"Order {placed.OrderId} sent: {description}");
			_ = FollowQuickOrderAsync(placed, _session);
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			ErrorMessage = $"Order not sent: {ErrorMessageHelpers.ToUserMessage(ex)}";
			Log(MessageSeverity.Error, $"{ErrorMessage} ({description})");
		}
		finally
		{
			_quickOrdersInFlight.Remove(key);
		}
	}

	// A rejection fails the status stream: shown in the chain's error line and the messages
	private async Task FollowQuickOrderAsync(PlacedOrderDto placed, CancellationToken session)
	{
		try
		{
			await foreach (
				var status in placed.Statuses.WithCancellation(session).ConfigureAwait(false)
			)
			{
				if (!status.IsDone)
					continue;
				var text =
					$"Order {placed.OrderId} {status.Status}: {FormatHelpers.Quantity(status.Filled)} filled @ {FormatHelpers.FixedPrice(status.AverageFillPrice)}";
				Dispatcher.UIThread.Post(() => ShowTradeStatus(text));
				Log(MessageSeverity.Info, text);
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			var text = $"Order {placed.OrderId} rejected: {ErrorMessageHelpers.ToUserMessage(ex)}";
			Dispatcher.UIThread.Post(() => ErrorMessage = text);
			Log(MessageSeverity.Error, text);
		}
	}

	private void ShowTradeStatus(string text)
	{
		TradeStatus = text;
		_tradeStatusTimer.Stop();
		_tradeStatusTimer.Start();
	}

	private void Log(MessageSeverity severity, string text) =>
		_messageLogManager.Add(severity, MessageLogConstants.SourceOrders, text);

	private void StartRowQuotes(OptionRowViewModel row, CancellationToken quotes)
	{
		var cts = CancellationTokenSource.CreateLinkedTokenSource(quotes);
		var tasks = new List<Task>(2);
		if (row.Strike.Call is { } call)
			tasks.Add(StartQuote(call, row.CallPending, cts.Token));
		if (row.Strike.Put is { } put)
			tasks.Add(StartQuote(put, row.PutPending, cts.Token));
		_rowQuotes[row] = new RowQuotes(cts, tasks);
	}

	// Moves the window to the price once the ATM strike drifted far enough off its centre. Rows
	// that stay keep their view models and streams; the lines of rows leaving are released
	// before the new rows subscribe, so the chain never holds more lines than a full window.
	private async Task RecenterAsync()
	{
		if (_chain is not { } chain || _recentering)
			return;
		if (
			OptionChainHelpers.GetRecenterTarget(
				chain.Strikes,
				_center,
				_underlyingPrice,
				MarketDataConstants.OptionStrikesAroundAtm,
				MarketDataConstants.OptionStrikesRecenterThreshold
			)
			is not { } target
		)
			return;

		_recentering = true;
		try
		{
			_center = target;
			var (from, to) = OptionChainHelpers.GetWindow(
				chain.Strikes.Count,
				target,
				MarketDataConstants.OptionStrikesAroundAtm
			);
			var window = chain.Strikes.Skip(from).Take(to - from + 1).ToList();
			var keep = window.ToHashSet();

			var leaving = new List<RowQuotes>();
			foreach (var row in Rows.Where(x => !keep.Contains(x.Strike)).ToList())
			{
				Rows.Remove(row);
				if (_rowQuotes.Remove(row, out var quotes))
				{
					quotes.Cancellation.Cancel();
					leaving.Add(quotes);
				}
			}
			var added = new List<OptionRowViewModel>();
			for (var i = 0; i < window.Count; i++)
			{
				if (i < Rows.Count && Rows[i].Strike == window[i])
					continue;
				var row = CreateRow(window[i]);
				Rows.Insert(i, row);
				added.Add(row);
			}
			ApplyMoneyness();
			if (Rows.FirstOrDefault(x => x.IsAtm) is { } atmRow)
				Recentered?.Invoke(atmRow);

			try
			{
				await Task.WhenAll(leaving.SelectMany(x => x.Tasks));
			}
			catch (OperationCanceledException) { }
			foreach (var quotes in leaving)
				quotes.Cancellation.Dispose();

			// A new chain, tab or disconnect while waiting owns the rows now
			if (_chain != chain || _quotes is not { IsCancellationRequested: false } live)
				return;
			foreach (var row in added.Where(Rows.Contains))
				StartRowQuotes(row, live.Token);
		}
		finally
		{
			_recentering = false;
		}

		// The price may have moved on while the old lines were released
		await RecenterAsync();
	}

	private void FlushUnderlyingPrice()
	{
		if (_chain == null || _underlyingPending.Take() is not { } quote)
			return;
		var price = TickTypeHelpers.GetDisplayPrice(quote) ?? _underlyingPrice;
		if (price == _underlyingPrice && quote.ImpliedVolatility == _impliedVolatility)
			return;
		_impliedVolatility = quote.ImpliedVolatility;
		ApplyUnderlyingPrice(price);
	}

	// Shades in-the-money calls and puts and marks the strike nearest the price
	private void ApplyUnderlyingPrice(double? price)
	{
		if (_chain == null)
			return;
		_underlyingPrice = price;
		ApplyMoneyness();
		_ = RecenterAsync();
	}

	// Also colours strikes by σ distance: 1σ = price × IV × √(years to the expiry's close)
	private void ApplyMoneyness()
	{
		if (_chain == null)
			return;
		var atm = OptionChainHelpers.GetAtmStrike(_chain.Strikes, _underlyingPrice);
		var years = _expiration is { } expiration
			? OptionChainHelpers.GetYearsToExpiration(expiration, _timeProvider.GetUtcNow())
			: 0;
		var sigma = OptionChainHelpers.GetExpectedMove(_underlyingPrice, _impliedVolatility, years);
		foreach (var row in Rows)
			row.SetUnderlyingPrice(_underlyingPrice, atm, sigma);

		var iv = _impliedVolatility is { } v
			? $" · IV {FormatHelpers.FixedPrice(v * 100)}%"
			: string.Empty;
		var move = sigma is { } s ? $" · 1σ ±{FormatHelpers.FixedPrice(s)}" : string.Empty;
		InfoText =
			$"Underlying {FormatHelpers.FixedPrice(_underlyingPrice)}{iv}{move} · {Rows.Count} of {_chain.Strikes.Count} strikes around the money";
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

	private Task StartQuote(
		ContractDto contract,
		LatestValue<QuoteDto> target,
		CancellationToken token,
		string genericTicks = ""
	) =>
		Task.Run(
			async () =>
			{
				try
				{
					await foreach (
						var quote in _marketDataManager
							.SubscribeQuotesAsync(
								contract,
								token,
								genericTicks,
								MarketDataStreamOwners.OptionChain
							)
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
		foreach (var quotes in _rowQuotes.Values)
			quotes.Cancellation.Dispose();
		_rowQuotes.Clear();
	}

	private sealed record RowQuotes(
		CancellationTokenSource Cancellation,
		IReadOnlyList<Task> Tasks
	);
}
