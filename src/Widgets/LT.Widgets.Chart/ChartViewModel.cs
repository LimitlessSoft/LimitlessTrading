using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Controls;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.Performance;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Charts;
using LT.Contracts.Requests.Contracts;

namespace LT.App.Widgets.Chart;

/// <summary>
/// One chart. Several can be open; each has its own timeframe and either follows the selected
/// contract (linked) or stays on the contract it shows (pinned). The selected account's recorded
/// fills of the contract are drawn on it (trade journal: socket executions and IB Flex), closed
/// quantity joined entry → exit, and the break-even of an open position (live positions only).
/// </summary>
public partial class ChartViewModel
	: ConnectedViewModelBase,
		IWidgetState,
		ILinkable,
		ITickerTitled,
		ITickerDropTarget
{
	private readonly IChartManager _chartManager;
	private readonly IContractManager _contractManager;
	private readonly IMessageLogManager _messageLogManager;
	private readonly ITradeJournalManager _tradeJournalManager;
	private readonly IPositionManager _positionManager;
	private readonly AccountStore _accountStore;
	private readonly ZonedText _status;
	private readonly Dictionary<(string Account, int ContractId), PositionDto> _positions = [];
	private int _tradesQueued;
	private CancellationTokenSource? _load;
	private ContractDto? _contract;

	public ChartViewModel(
		IChartManager chartManager,
		IContractManager contractManager,
		IConnectionManager connectionManager,
		IMessageLogManager messageLogManager,
		ISettingsManager settingsManager,
		ITradeJournalManager tradeJournalManager,
		IPositionManager positionManager,
		AccountStore accountStore,
		LinkGroupStore linkGroupStore,
		DisplayTimeStore displayTime
	)
		: base(connectionManager)
	{
		DisplayTime = displayTime;
		_status = new ZonedText(x => StatusText = x);
		displayTime.ZoneChanged += _status.Rebuild;
		_chartManager = chartManager;
		_contractManager = contractManager;
		_messageLogManager = messageLogManager;
		_tradeJournalManager = tradeJournalManager;
		_positionManager = positionManager;
		_accountStore = accountStore;
		tradeJournalManager.FillsChanged += OnFillsChanged;
		accountStore.PropertyChanged += OnAccountChanged;
		Link = new ContractLink(linkGroupStore, () => _contract);
		Link.ContractReceived += OnContractReceived;
		TickerEditor = new TickerTitleEditor(
			contractManager,
			() => IsConnected,
			OpenTickerAsync,
			OpenContract
		);

		Timeframes = Enum.GetValues<ChartTimeframe>().Select(x => new TimeframeOption(x)).ToList();
		var saved = settingsManager.GetLayout().ChartTimeframe;
		SelectedTimeframe = Timeframes.FirstOrDefault(x => x.Value == saved) ?? Timeframes[1];

		_contract = linkGroupStore.Get(Link.Group);
		Reload();
	}

	public ContractLink Link { get; }

	/// <summary>The display time zone; the chart draws its time axis in it.</summary>
	public DisplayTimeStore DisplayTime { get; }

	/// <summary>The header title's ticker box (<see cref="LT.App.Controls.TickerTitle"/>).</summary>
	public TickerTitleEditor TickerEditor { get; }

	/// <summary>Ticker box: resolves the ticker and pins this chart to it.</summary>
	public async Task OpenTickerAsync(string ticker)
	{
		ErrorMessage = null;
		try
		{
			ShowContract(await _contractManager.ResolveTickerAsync(ticker));
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	/// <summary>Ticker box: a recent contract, already resolved.</summary>
	public void OpenContract(ContractDto contract)
	{
		_contractManager.AddRecentContract(contract);
		ShowContract(contract);
	}

	/// <summary>
	/// A ticker dragged onto this chart (from a watchlist, positions, orders or executions row):
	/// shown here like a picked one and added to the recent tickers, but only this chart switches —
	/// the link group keeps its contract, so other charts stay put. The chart stays in its group
	/// and follows the group's next contract.
	/// </summary>
	public void DropContract(ContractDto contract)
	{
		ErrorMessage = null;
		_contractManager.AddRecentContract(contract);
		if (contract.ConId == _contract?.ConId)
			return;
		_contract = contract;
		Reload();
	}

	// A ticker picked here moves every widget in this chart's link group along
	private void ShowContract(ContractDto contract)
	{
		_contract = contract;
		Reload();
		Link.Publish(contract);
	}

	private void OnContractReceived(ContractDto contract)
	{
		if (contract.ConId == _contract?.ConId)
			return;
		_contract = contract;
		Reload();
	}

	public void LoadState(WidgetPlacementDto placement)
	{
		if (placement.ChartTimeframe is { } timeframe)
			SelectedTimeframe =
				Timeframes.FirstOrDefault(x => x.Value == timeframe) ?? SelectedTimeframe;
		// Layouts saved before link groups: a chart pinned to its own ticker is not linked
		if (!placement.ChartLinked)
			Link.Group = LinkGroup.None;
		RightMargin = Math.Max(0, placement.ChartRightMargin ?? 0);
		if (placement.ChartContract != null)
			ShowContract(placement.ChartContract);
	}

	public void SaveState(WidgetPlacementDto placement)
	{
		placement.ChartTimeframe = SelectedTimeframe.Value;
		placement.ChartLinked = Link.Group != LinkGroup.None;
		placement.ChartContract = _contract;
		placement.ChartRightMargin = RightMargin > 0 ? RightMargin : null;
	}

	public override void Dispose()
	{
		_tradeJournalManager.FillsChanged -= OnFillsChanged;
		_accountStore.PropertyChanged -= OnAccountChanged;
		DisplayTime.ZoneChanged -= _status.Rebuild;
		Link.Dispose();
		_load?.Cancel();
		_load?.Dispose();
		_load = null;
		base.Dispose();
	}

	public IReadOnlyList<TimeframeOption> Timeframes { get; }

	[ObservableProperty]
	public partial TimeframeOption SelectedTimeframe { get; set; }

	[ObservableProperty]
	public partial bool UseRegularTradingHours { get; set; } = true;

	[ObservableProperty]
	public partial IReadOnlyList<BarDto> Bars { get; set; } = [];

	[ObservableProperty]
	public partial bool IsIntraday { get; set; }

	/// <summary>Empty bars after the latest one; set by dragging the chart's margin handle.</summary>
	[ObservableProperty]
	public partial double RightMargin { get; set; }

	/// <summary>The account's recorded fills of the charted contract, oldest first.</summary>
	[ObservableProperty]
	public partial IReadOnlyList<ExecutionDto> Fills { get; set; } = [];

	/// <summary>Entry → exit pairs of the closed quantity (FIFO).</summary>
	[ObservableProperty]
	public partial IReadOnlyList<TradeLegDto> TradeLegs { get; set; } = [];

	/// <summary>Break-even price of the open position in the charted contract; null when flat.</summary>
	[ObservableProperty]
	public partial double? BreakEvenPrice { get; set; }

	[ObservableProperty]
	public partial string Title { get; set; } = "Chart";

	[ObservableProperty]
	public partial string? StatusText { get; set; }

	[ObservableProperty]
	public partial bool IsLoading { get; set; }

	/// <summary>Header timeframe menu: switches the bar size and reloads.</summary>
	public void SetTimeframe(ChartTimeframe timeframe)
	{
		if (timeframe != SelectedTimeframe.Value)
			SelectedTimeframe =
				Timeframes.FirstOrDefault(x => x.Value == timeframe) ?? SelectedTimeframe;
	}

	partial void OnSelectedTimeframeChanged(TimeframeOption value) => Reload();

	partial void OnUseRegularTradingHoursChanged(bool value) => Reload();

	protected override void OnConnected(CancellationToken session)
	{
		Reload();
		_ = StreamPositionsAsync(session);
	}

	protected override void OnDisconnected()
	{
		_load?.Cancel();
		_positions.Clear();
		UpdateBreakEven();
	}

	[RelayCommand]
	public void Reload()
	{
		LoadTrades();
		UpdateBreakEven();
		_load?.Cancel();
		_load?.Dispose();
		_load = new CancellationTokenSource();
		_ = LoadAsync(_load.Token);
	}

	private void OnAccountChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(AccountStore.SelectedAccount))
			return;
		LoadTrades();
		UpdateBreakEven();
	}

	// From the trade journal's threads; coalesced to one UI update per burst
	private void OnFillsChanged()
	{
		if (Interlocked.Exchange(ref _tradesQueued, 1) == 0)
			Dispatcher.UIThread.Post(() =>
			{
				Interlocked.Exchange(ref _tradesQueued, 0);
				LoadTrades();
			});
	}

	private void LoadTrades()
	{
		if (_contract is not { } contract)
		{
			Fills = [];
			TradeLegs = [];
			return;
		}
		var trades = _tradeJournalManager.GetContractTrades(
			contract.ConId,
			_accountStore.SelectedAccount
		);
		Fills = trades.Fills;
		TradeLegs = trades.Legs;
	}

	// Low-frequency stream: consumed on the UI thread (the continuation comes back on it)
	private async Task StreamPositionsAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (
				var position in _positionManager.SubscribePositionsAsync(cancellationToken)
			)
			{
				var key = (position.Account, position.ContractId);
				if (position.Quantity == 0)
					_positions.Remove(key);
				else
					_positions[key] = position;
				if (position.ContractId == _contract?.ConId)
					UpdateBreakEven();
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			_messageLogManager.Add(
				MessageSeverity.Info,
				MessageLogConstants.SourceChart,
				$"Break-even line unavailable: positions failed ({ErrorMessageHelpers.ToUserMessage(ex)})"
			);
		}
	}

	private void UpdateBreakEven()
	{
		var account = _accountStore.SelectedAccount;
		var position = _contract is { } contract
			? _positions.Values.FirstOrDefault(x =>
				x.ContractId == contract.ConId && (account == null || x.Account == account)
			)
			: null;
		BreakEvenPrice = position != null ? ChartTradeHelpers.GetBreakEvenPrice(position) : null;
	}

	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		var contract = _contract;
		ErrorMessage = null;
		_status.Clear();
		if (contract == null)
		{
			Title = "Chart";
			Bars = [];
			_status.SetFixed("Select a contract in the watchlist or positions.");
			return;
		}

		var timeframe = SelectedTimeframe.Value;
		var request = new GetBarsRequest
		{
			Contract = contract,
			Timeframe = timeframe,
			UseRegularTradingHours = UseRegularTradingHours,
		};
		Title = ContractHelpers.GetDisplayName(contract);
		IsIntraday = ChartTimeframeHelpers.IsIntraday(timeframe);
		IsLoading = true;
		try
		{
			var result = await _chartManager.GetBarsAsync(request, cancellationToken);
			Bars = result.Bars;
			_status.Set(() =>
				result.IsStale
					? $"Cached {DisplayTime.Format(result.FetchedAt, "HH:mm")} — {result.Error}"
					: $"Loaded {DisplayTime.Format(result.FetchedAt)}"
			);
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception ex)
		{
			Bars = [];
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
			return;
		}
		finally
		{
			IsLoading = false;
		}

		if (!IsConnected)
			return;

		try
		{
			await foreach (
				var update in _chartManager.SubscribeRealtimeBarsAsync(request, cancellationToken)
			)
			{
				Bars = BarHelpers.Merge(Bars, update, timeframe);
				var time = update.Time;
				_status.Set(() => $"Live · {DisplayTime.Format(time)}");
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			var message = ErrorMessageHelpers.ToUserMessage(ex);
			_status.Append(" · real-time updates unavailable");
			_messageLogManager.Add(
				MessageSeverity.Info,
				MessageLogConstants.SourceChart,
				$"{ContractHelpers.GetDisplayName(contract)}: real-time bars unavailable ({message})"
			);
		}
	}
}
