using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LSCore.Exceptions;
using LT.App.Controls;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Dtos.Performance;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Charts;
using LT.Contracts.Requests.Contracts;
using LT.Contracts.Requests.Indicators;

namespace LT.App.Widgets.Chart;

/// <summary>
/// One chart. Several can be open; each has its own timeframe, period and trading hours (regular
/// only or with pre- and post-market bars) and either follows the selected
/// contract (linked) or stays on the contract it shows (pinned). The selected account's recorded
/// fills of the contract are drawn on it (trade journal: socket executions and IB Flex), closed
/// quantity joined entry → exit, and the break-even of an open position (live positions only).
/// <para>
/// The period is a zoom preset, not a filter: the chart loads at least the timeframe's own span
/// and fits its view to the period (<see cref="PeriodStart"/>); zooming or scrolling away shows
/// "Custom" in the header until a period is picked again or the chart is double-clicked.
/// Scrolling or zooming to the oldest bar loads the page of history before it
/// (<see cref="LoadOlderBars"/>), one page at a time, until IB has no more.
/// </para>
/// <para>
/// Its indicators (saved with the chart) are computed on the thread pool by
/// <see cref="IIndicatorManager"/> whenever the bars, the indicator list or a script they use
/// change — at most every <see cref="IndicatorConstants.ComputeDebounce"/>, one compute at a
/// time — and posted back to the UI thread.
/// </para>
/// <para>
/// Its setup (timeframe, period, indicators, right margin) can be saved as a named template and
/// applied from the header, and can follow its template (changes saved to it and taken from it); see
/// ChartViewModel.Templates.cs.
/// </para>
/// <para>
/// The contract's price alerts are drawn on it and added, moved and removed from it; see
/// ChartViewModel.Alerts.cs. The toolbox bar above the plot (shown or hidden per chart) arms
/// tools such as placing an alert with a click; see ChartViewModel.Toolbox.cs. Its Draw button
/// turns on drawing mode: trend lines, shapes, Fibonacci levels, measures and notes, kept per
/// contract; see ChartViewModel.Drawings.cs.
/// </para>
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
	private readonly IIndicatorManager _indicatorManager;
	private readonly IChartTemplateManager _chartTemplateManager;
	private readonly ISettingsManager _settingsManager;
	private readonly TimeProvider _timeProvider;
	private readonly DispatcherTimer _computeTimer;
	private readonly DispatcherTimer _templateSaveTimer;
	private List<IndicatorInstanceDto> _indicators = [];
	private bool _computing;
	private bool _computePending;
	private bool _disposed;
	private readonly AccountStore _accountStore;
	private readonly ZonedText _status;
	private readonly Dictionary<(string Account, int ContractId), PositionDto> _positions = [];
	private int _tradesQueued;
	private CancellationTokenSource? _load;
	private ContractDto? _contract;

	// The current load's result (for the status line) and its live bars while they stream
	private ChartBarsDto? _loaded;
	private ChartLiveBars? _live;

	// Older history of the current load: a page on its way, none left, no retry before
	private bool _olderLoading;
	private bool _olderExhausted;
	private DateTimeOffset? _olderRetryAt;

	// A period was picked while its bars were loading: fit the view to it once they are in
	private bool _resetViewOnLoad;
	private ChartLiveUpdateMode _liveMode;
	private TimeSpan _redrawInterval;

	// The regular sessions last read, by contract: a reload of the same contract reuses them
	private (int ConId, TradingScheduleDto? Schedule)? _regularHours;

	public ChartViewModel(
		IChartManager chartManager,
		IContractManager contractManager,
		IConnectionManager connectionManager,
		IMessageLogManager messageLogManager,
		ISettingsManager settingsManager,
		ITradeJournalManager tradeJournalManager,
		IPositionManager positionManager,
		IIndicatorManager indicatorManager,
		IChartTemplateManager chartTemplateManager,
		IPriceAlertManager priceAlertManager,
		IChartDrawingManager chartDrawingManager,
		AccountStore accountStore,
		LinkGroupStore linkGroupStore,
		DisplayTimeStore displayTime,
		TimeProvider timeProvider
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
		_indicatorManager = indicatorManager;
		_chartTemplateManager = chartTemplateManager;
		_priceAlertManager = priceAlertManager;
		_chartDrawingManager = chartDrawingManager;
		_settingsManager = settingsManager;
		_timeProvider = timeProvider;
		_accountStore = accountStore;
		_computeTimer = new DispatcherTimer { Interval = IndicatorConstants.ComputeDebounce };
		_computeTimer.Tick += (_, _) => ComputeIndicators();
		_templateSaveTimer = new DispatcherTimer
		{
			Interval = ChartTemplateConstants.FollowSaveDebounce,
		};
		_templateSaveTimer.Tick += (_, _) => SaveFollowedTemplate();
		chartTemplateManager.TemplateSaved += OnTemplateSaved;
		indicatorManager.DefinitionsChanged += OnIndicatorDefinitionsChanged;
		tradeJournalManager.FillsChanged += OnFillsChanged;
		priceAlertManager.AlertsChanged += OnAlertsChanged;
		chartDrawingManager.DrawingsChanged += OnDrawingsChangedElsewhere;
		accountStore.PropertyChanged += OnAccountChanged;
		settingsManager.AppSettingsChanged += OnAppSettingsChanged;
		Link = new ContractLink(linkGroupStore, () => _contract);
		Link.ContractReceived += OnContractReceived;
		TickerEditor = new TickerTitleEditor(
			contractManager,
			() => IsConnected,
			OpenTickerAsync,
			OpenContract
		);

		displayTime.ZoneChanged += OnZoneChanged;
		var layout = settingsManager.GetLayout();
		Periods = Enum.GetValues<ChartPeriod>().Select(x => new PeriodOption(x)).ToList();
		SelectedPeriod = Periods.FirstOrDefault(x => x.Value == layout.ChartPeriod) ?? Periods[0];
		Timeframes = Enum.GetValues<ChartTimeframe>().Select(x => new TimeframeOption(x)).ToList();
		var saved = layout.ChartTimeframe;
		SelectedTimeframe = Timeframes.FirstOrDefault(x => x.Value == saved) ?? Timeframes[1];
		// A new chart starts from the default template; a restored one then loads its own state
		if (chartTemplateManager.GetDefault() is { } template)
		{
			ApplyTemplateSettings(template.Settings);
			SetAppliedTemplate(template);
		}

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
		// Loading the chart's own setup is not an edit to save to a template
		StopFollowingTemplate();
		if (placement.ChartTimeframe is { } timeframe)
			SelectedTimeframe =
				Timeframes.FirstOrDefault(x => x.Value == timeframe) ?? SelectedTimeframe;
		SelectedPeriod =
			Periods.FirstOrDefault(x => x.Value == (placement.ChartPeriod ?? ChartPeriod.Auto))
			?? SelectedPeriod;
		UseRegularTradingHours = !(placement.ChartOutsideRegularHours ?? true);
		// Layouts saved before link groups: a chart pinned to its own ticker is not linked
		if (!placement.ChartLinked)
			Link.Group = LinkGroup.None;
		RightMargin = Math.Max(0, placement.ChartRightMargin ?? 0);
		ShowToolbox = placement.ChartShowToolbox ?? true;
		IsDrawingMode = placement.ChartDrawingMode ?? false;
		DrawingsLocked = placement.ChartDrawingsLocked ?? false;
		DrawingsHidden = placement.ChartDrawingsHidden ?? false;
		_indicators = [.. placement.ChartIndicators ?? []];
		RefreshChartIndicators();
		ScheduleIndicators();
		LoadTemplateState(placement);
		if (placement.ChartContract != null)
			ShowContract(placement.ChartContract);
	}

	public void SaveState(WidgetPlacementDto placement)
	{
		placement.ChartTimeframe = SelectedTimeframe.Value;
		placement.ChartPeriod = SelectedPeriod.Value;
		placement.ChartOutsideRegularHours = !UseRegularTradingHours;
		placement.ChartLinked = Link.Group != LinkGroup.None;
		placement.ChartContract = _contract;
		placement.ChartRightMargin = RightMargin > 0 ? RightMargin : null;
		placement.ChartIndicators = _indicators.Count > 0 ? [.. _indicators] : null;
		placement.ChartTemplateId = _templateId;
		placement.ChartFollowTemplate = FollowTemplate;
		placement.ChartShowToolbox = ShowToolbox;
		placement.ChartDrawingMode = IsDrawingMode ? true : null;
		placement.ChartDrawingsLocked = DrawingsLocked ? true : null;
		placement.ChartDrawingsHidden = DrawingsHidden ? true : null;
	}

	public override void Dispose()
	{
		// An edit still waiting for its save is saved now
		FlushTemplateSave();
		_disposed = true;
		_computeTimer.Stop();
		_templateSaveTimer.Stop();
		_chartTemplateManager.TemplateSaved -= OnTemplateSaved;
		_indicatorManager.DefinitionsChanged -= OnIndicatorDefinitionsChanged;
		_tradeJournalManager.FillsChanged -= OnFillsChanged;
		_priceAlertManager.AlertsChanged -= OnAlertsChanged;
		_chartDrawingManager.DrawingsChanged -= OnDrawingsChangedElsewhere;
		_accountStore.PropertyChanged -= OnAccountChanged;
		_settingsManager.AppSettingsChanged -= OnAppSettingsChanged;
		DisplayTime.ZoneChanged -= _status.Rebuild;
		DisplayTime.ZoneChanged -= OnZoneChanged;
		Link.Dispose();
		_load?.Cancel();
		_load?.Dispose();
		_load = null;
		base.Dispose();
	}

	public IReadOnlyList<TimeframeOption> Timeframes { get; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasTradingHoursToggle))]
	public partial TimeframeOption SelectedTimeframe { get; set; }

	/// <summary>Header period menu: Auto, then the rolling and the calendar periods.</summary>
	public IReadOnlyList<PeriodOption> Periods { get; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(
		nameof(FitsPeriod),
		nameof(IsPeriodCustom),
		nameof(PeriodLabel),
		nameof(PeriodToolTip)
	)]
	public partial PeriodOption SelectedPeriod { get; set; }

	/// <summary>
	/// A chosen period is shown whole (the chart zooms to fit its bars from
	/// <see cref="PeriodStart"/>); Auto shows the latest bars at the default zoom, as before
	/// periods.
	/// </summary>
	public bool FitsPeriod => SelectedPeriod.Value != ChartPeriod.Auto;

	/// <summary>
	/// Where the chosen period starts (capped at the timeframe's longest); the chart fits its view
	/// from here to the latest bar. Null for Auto.
	/// </summary>
	[ObservableProperty]
	public partial DateTimeOffset? PeriodStart { get; set; }

	/// <summary>
	/// The chart's view is fitted to the period; the chart clears it when the view is zoomed,
	/// panned or its margin dragged, and sets it on a reset (double-click, period picked).
	/// </summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsPeriodCustom), nameof(PeriodLabel), nameof(PeriodToolTip))]
	public partial bool IsPeriodFitted { get; set; } = true;

	/// <summary>
	/// The view left the chosen period (zoomed or scrolled away): the header says "Custom" and
	/// the period menu checks nothing. Never for Auto, which is no span.
	/// </summary>
	public bool IsPeriodCustom => FitsPeriod && !IsPeriodFitted;

	/// <summary>Header period button: the period, or "Custom" once the view left it.</summary>
	public string PeriodLabel =>
		IsPeriodCustom ? ChartPeriodHelpers.CustomDisplayName : SelectedPeriod.ToString();

	public string PeriodToolTip =>
		IsPeriodCustom
			? $"Zoomed or scrolled away from {SelectedPeriod}. Click to pick a period (picking {SelectedPeriod} again shows it whole)"
			: "Click to change the period";

	/// <summary>
	/// Asks the chart to fit its view to the period again (ChartView calls
	/// CandlestickChart.ResetView): a period was picked, also the one already chosen.
	/// </summary>
	public event Action? ViewResetRequested;

	/// <summary>
	/// Regular hours only (IB useRTH). Each chart has its own, saved with it; off by default, so
	/// intraday charts include pre- and post-market bars. Toggled from the header.
	/// </summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(TradingHoursLabel), nameof(TradingHoursToolTip))]
	public partial bool UseRegularTradingHours { get; set; }

	/// <summary>
	/// Header trading hours toggle shown: intraday timeframes only (daily and weekly bars are
	/// always regular hours).
	/// </summary>
	public bool HasTradingHoursToggle => ChartTimeframeHelpers.IsIntraday(SelectedTimeframe.Value);

	/// <summary>Header trading hours toggle: ETH with pre- and post-market bars, RTH without.</summary>
	public string TradingHoursLabel => UseRegularTradingHours ? "RTH" : "ETH";

	public string TradingHoursToolTip =>
		UseRegularTradingHours
			? "Regular hours only. Click to show pre- and post-market bars"
			: "Pre- and post-market bars shown (hollow, on a lighter background). Click for regular hours only";

	/// <summary>Header trading hours toggle: switches this chart between RTH and ETH and reloads it.</summary>
	[RelayCommand]
	public void ToggleTradingHours() => UseRegularTradingHours = !UseRegularTradingHours;

	/// <summary>
	/// The contract's regular sessions while pre- / post-market bars are shown on an intraday
	/// chart (the chart shades the bars outside them); null otherwise or when IB has none.
	/// </summary>
	[ObservableProperty]
	public partial TradingScheduleDto? RegularHours { get; set; }

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

	partial void OnSelectedTimeframeChanged(TimeframeOption value)
	{
		ReloadUnlessApplyingTemplate();
		OnTemplateSettingsEdited();
	}

	/// <summary>
	/// Header period menu: zooms the view to the period (from its start to now). The bars already
	/// loaded are kept when they reach back to its start; otherwise the chart reloads with enough
	/// history. Picking the period already chosen fits the view to it again (not while a template
	/// is applied: following a template must not move a zoomed view).
	/// </summary>
	public void SetPeriod(ChartPeriod period)
	{
		if (period != SelectedPeriod.Value)
			SelectedPeriod = Periods.FirstOrDefault(x => x.Value == period) ?? SelectedPeriod;
		else if (!_applyingTemplate)
			ResetView();
	}

	partial void OnSelectedPeriodChanged(PeriodOption value)
	{
		if (!ApplyPeriodToLoadedBars())
		{
			_resetViewOnLoad = true;
			ReloadUnlessApplyingTemplate();
		}
		else
			ResetView();
		OnTemplateSettingsEdited();
	}

	// Today, this week… start at midnight in the display zone: another zone moves them
	private void OnZoneChanged()
	{
		if (ChartPeriodHelpers.IsCalendar(SelectedPeriod.Value) && !ApplyPeriodToLoadedBars())
			Reload();
	}

	// The period's start on the bars already loaded, when they cover it (a bar before it, or no
	// older history); false when the chart has to load more (or is loading, or has nothing)
	private bool ApplyPeriodToLoadedBars()
	{
		if (IsLoading || _loaded == null || Bars.Count == 0)
			return false;
		var range = ChartPeriodHelpers.GetRange(
			SelectedPeriod.Value,
			SelectedTimeframe.Value,
			_timeProvider.GetUtcNow(),
			DisplayTime.Zone
		);
		if (
			range.From is { } from
			&& !_olderExhausted
			&& ChartPeriodHelpers.GetFirstIndex(
				Bars,
				from,
				SelectedTimeframe.Value,
				DisplayTime.Zone
			) == 0
		)
			return false;
		PeriodStart = range.From;
		_loaded = new ChartBarsDto
		{
			FetchedAt = _loaded.FetchedAt,
			IsStale = _loaded.IsStale,
			Error = _loaded.Error,
			From = range.From,
			LimitedTo = range.LimitedTo,
		};
		SetLoadedStatus();
		return true;
	}

	// The view back to the period: fitted from its start (Auto: the latest bars, default zoom)
	private void ResetView()
	{
		IsPeriodFitted = true;
		ViewResetRequested?.Invoke();
	}

	partial void OnRightMarginChanged(double value) => OnTemplateSettingsEdited();

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
		LoadAlerts();
		// A pending "Remove all" named the symbol shown before
		IsConfirmingRemoveAllDrawings = false;
		LoadDrawings();
		RefreshTools();
		UpdateBreakEven();
		_load?.Cancel();
		_load?.Dispose();
		_load = new CancellationTokenSource();
		_loaded = null;
		_olderLoading = false;
		_olderExhausted = false;
		_olderRetryAt = null;
		_ = LoadAsync(_load.Token);
	}

	/// <summary>
	/// The chart's view nears the oldest bar (CandlestickChart.OlderBarsNeeded): loads the page of
	/// history before it and puts it in front, the view staying where it is. One page at a time;
	/// none while loading, disconnected, after IB had no more (start of history) or once the
	/// chart holds <see cref="ChartConstants.MaxLoadedBars"/>; a failed page is retried no sooner
	/// than <see cref="ChartConstants.OlderBarsRetryDelay"/>. Cancelled with the load (another
	/// contract, timeframe, trading hours, a reload, a disconnect).
	/// </summary>
	public void LoadOlderBars()
	{
		if (
			_olderLoading
			|| _olderExhausted
			|| IsLoading
			|| !IsConnected
			|| _loaded == null
			|| _contract is not { } contract
			|| _load is not { } load
			|| Bars.Count == 0
		)
			return;
		if (Bars.Count >= ChartConstants.MaxLoadedBars)
		{
			_olderExhausted = true;
			return;
		}
		if (_olderRetryAt is { } retryAt && _timeProvider.GetUtcNow() < retryAt)
			return;
		_ = LoadOlderAsync(contract, load.Token);
	}

	// On the UI thread (the continuation comes back on it)
	private async Task LoadOlderAsync(ContractDto contract, CancellationToken cancellationToken)
	{
		_olderLoading = true;
		var before = Bars[0].Time;
		try
		{
			var older = await _chartManager.GetOlderBarsAsync(
				new GetOlderBarsRequest
				{
					Contract = contract,
					Timeframe = SelectedTimeframe.Value,
					UseRegularTradingHours = UseRegularTradingHours,
					Before = before,
				},
				cancellationToken
			);
			cancellationToken.ThrowIfCancellationRequested();
			_olderRetryAt = null;
			if (_live != null)
				_live.Prepend(older);
			else
				Bars = BarHelpers.Prepend(older, Bars);
			// Nothing older came back: the start of the contract's history
			if (Bars.Count == 0 || Bars[0].Time >= before)
				_olderExhausted = true;
		}
		catch (OperationCanceledException) { }
		catch (Exception) when (cancellationToken.IsCancellationRequested) { }
		catch (Exception ex)
		{
			_olderRetryAt = _timeProvider.GetUtcNow() + ChartConstants.OlderBarsRetryDelay;
			_messageLogManager.Add(
				MessageSeverity.Info,
				MessageLogConstants.SourceChart,
				$"{ContractHelpers.GetDisplayName(contract)}: older bars unavailable ({ErrorMessageHelpers.ToUserMessage(ex)})"
			);
		}
		finally
		{
			// A reload resets it for its own load
			if (!cancellationToken.IsCancellationRequested)
				_olderLoading = false;
		}
	}

	private void OnAccountChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(AccountStore.SelectedAccount))
			return;
		LoadTrades();
		UpdateBreakEven();
	}

	// From the thread that saved the settings. Live-update changes reload
	private void OnAppSettingsChanged(AppSettingsDto settings) =>
		Dispatcher.UIThread.Post(() =>
		{
			if (_disposed)
				return;
			if (
				(
					settings.ChartLiveUpdateMode == _liveMode
					&& ChartLiveUpdateHelpers.GetRedrawInterval(
						settings.ChartLiveUpdateMode,
						settings.ChartRedrawIntervalMs
					) == _redrawInterval
				)
			)
				return;
			Reload();
		});

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
		var settings = _settingsManager.GetAppSettings();
		_liveMode = settings.ChartLiveUpdateMode;
		_redrawInterval = ChartLiveUpdateHelpers.GetRedrawInterval(
			settings.ChartLiveUpdateMode,
			settings.ChartRedrawIntervalMs
		);
		var contract = _contract;
		ErrorMessage = null;
		_status.Clear();
		if (contract == null)
		{
			Title = "Chart";
			Bars = [];
			RegularHours = null;
			_status.SetFixed("Select a contract in the watchlist or positions.");
			return;
		}

		var timeframe = SelectedTimeframe.Value;
		var request = new GetBarsRequest
		{
			Contract = contract,
			Timeframe = timeframe,
			Period = SelectedPeriod.Value,
			TimeZone = DisplayTime.Zone,
			UseRegularTradingHours = UseRegularTradingHours,
		};
		Title = ContractHelpers.GetDisplayName(contract);
		IsIntraday = ChartTimeframeHelpers.IsIntraday(timeframe);
		RegularHours =
			!UseRegularTradingHours
			&& IsIntraday
			&& _regularHours is { } known
			&& known.ConId == contract.ConId
				? known.Schedule
				: null;
		IsLoading = true;
		try
		{
			var result = await _chartManager.GetBarsAsync(request, cancellationToken);
			// Superseded (another timeframe, period or contract) while IB answered: the newer
			// load's bars stay
			cancellationToken.ThrowIfCancellationRequested();
			// The fit's start before the bars: the chart fits a new series to it at once
			PeriodStart = result.From;
			Bars = result.Bars;
			_loaded = result;
			SetLoadedStatus();
			if (_resetViewOnLoad)
			{
				_resetViewOnLoad = false;
				ResetView();
			}
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception) when (cancellationToken.IsCancellationRequested)
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
		await StreamLiveAsync(request, contract, cancellationToken);
	}

	// "Loaded 14:05" (or the cached copy's error), and what limits the period's view
	private void SetLoadedStatus()
	{
		if (_loaded is not { } result)
			return;
		var period = SelectedPeriod.ToString();
		var timeframe = SelectedTimeframe.ToString();
		var noBars =
			result.From is { } start
			&& ChartPeriodHelpers.GetFirstIndex(
				Bars,
				start,
				SelectedTimeframe.Value,
				DisplayTime.Zone
			) == Bars.Count;
		_status.Set(() =>
			(
				result.IsStale
					? $"Cached {DisplayTime.Format(result.FetchedAt, "HH:mm")} — {result.Error}"
					: $"Loaded {DisplayTime.Format(result.FetchedAt)}"
			)
			+ (
				result.LimitedTo is { } limit
					? $" · {timeframe} periods go back {ChartPeriodHelpers.GetDisplayName(limit)} at most"
					: ""
			)
			+ (
				noBars && result.From is { } from
					? $" · no bars since {DisplayTime.FormatDayTime(from)} ({period})"
					: ""
			)
		);
	}

	// 5-second bars on the UI thread, plus live prices on the thread pool when the mode asks for
	// them; the prices end with the 5-second bars (a quote line is useless without them)
	private async Task StreamLiveAsync(
		GetBarsRequest request,
		ContractDto contract,
		CancellationToken cancellationToken
	)
	{
		var mode = _liveMode;
		using var live = new ChartLiveBars(
			Bars,
			request.Timeframe,
			_timeProvider,
			_redrawInterval,
			bars =>
			{
				if (!cancellationToken.IsCancellationRequested)
					Bars = bars;
			}
		);
		// Older pages scrolled to go through it, so its next update keeps them
		_live = live;
		using var prices = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var pricesToken = prices.Token;
		var note = new LiveNote();
		if (ChartLiveUpdateHelpers.UsesLivePrices(mode))
			_ = Task.Run(
				() => StreamLivePricesAsync(request, mode, live, note, pricesToken),
				CancellationToken.None
			);

		if (!UseRegularTradingHours && IsIntraday && _regularHours?.ConId != contract.ConId)
			await LoadRegularHoursAsync(contract, cancellationToken);

		try
		{
			await foreach (
				var update in _chartManager.SubscribeRealtimeBarsAsync(request, cancellationToken)
			)
			{
				live.AddRealtimeBar(update);
				var time = update.Time;
				_status.Set(() => $"Live · {DisplayTime.Format(time)}{note.Text}");
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
		finally
		{
			prices.Cancel();
			if (_live == live)
				_live = null;
		}
	}

	// Thread pool: a failed price stream (no tick-by-tick subscription or market data line left,
	// IB refused it) falls back — tick-by-tick to quotes, quotes to 5-second bars only — and says
	// so in the message log and the status line
	private async Task StreamLivePricesAsync(
		GetBarsRequest request,
		ChartLiveUpdateMode mode,
		ChartLiveBars live,
		LiveNote note,
		CancellationToken cancellationToken
	)
	{
		ChartLiveUpdateMode? current = mode;
		while (current is { } source)
		{
			var text = $" · {ChartLiveUpdateHelpers.GetSourceName(source)}";
			Dispatcher.UIThread.Post(() => note.Text = text);
			try
			{
				await foreach (
					var price in _chartManager
						.SubscribeLivePricesAsync(request, source, cancellationToken)
						.ConfigureAwait(false)
				)
					live.AddPrice(price);
				return;
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception ex)
			{
				if (cancellationToken.IsCancellationRequested)
					return;
				current = ChartLiveUpdateHelpers.GetFallback(source);
				var next = current is { } fallback
					? ChartLiveUpdateHelpers.GetSourceName(fallback)
					: "5-second bars";
				_messageLogManager.Add(
					MessageSeverity.Info,
					MessageLogConstants.SourceChart,
					$"{ContractHelpers.GetDisplayName(request.Contract)}: {ChartLiveUpdateHelpers.GetSourceName(source)} unavailable, using {next} ({ErrorMessageHelpers.ToUserMessage(ex)})"
				);
			}
		}
		Dispatcher.UIThread.Post(() => note.Text = " · 5-second bars only");
	}

	// What the live status line adds after its time (a fallback); written and read on the UI thread
	private sealed class LiveNote
	{
		public string Text { get; set; } = string.Empty;
	}

	// Without them every bar draws as regular; the chart itself still works
	private async Task LoadRegularHoursAsync(
		ContractDto contract,
		CancellationToken cancellationToken
	)
	{
		try
		{
			var schedule = await _chartManager.GetRegularHoursAsync(contract, cancellationToken);
			_regularHours = (contract.ConId, schedule);
			RegularHours = schedule;
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			_messageLogManager.Add(
				MessageSeverity.Info,
				MessageLogConstants.SourceChart,
				$"{ContractHelpers.GetDisplayName(contract)}: trading hours unavailable, pre- and post-market bars are not marked ({ErrorMessageHelpers.ToUserMessage(ex)})"
			);
		}
	}

	/// <summary>The chart's indicators as last computed, in legend order.</summary>
	[ObservableProperty]
	public partial IReadOnlyList<IndicatorResultDto> IndicatorResults { get; set; } = [];

	/// <summary>Indicator picker: the search text.</summary>
	[ObservableProperty]
	public partial string? IndicatorSearch { get; set; }

	/// <summary>Indicator picker: built-ins and scripts matching the search.</summary>
	[ObservableProperty]
	public partial IReadOnlyList<IndicatorChoiceItem> IndicatorChoices { get; set; } = [];

	/// <summary>Indicator manager: the indicators on this chart, in legend order.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasChartIndicators))]
	public partial IReadOnlyList<ChartIndicatorItem> ChartIndicators { get; set; } = [];

	public bool HasChartIndicators => ChartIndicators.Count > 0;

	public bool CanAddIndicator => _indicators.Count < IndicatorConstants.MaxIndicatorsPerChart;

	/// <summary>Indicator manager: shown instead of the picker when the chart is full.</summary>
	public string IndicatorLimitMessage =>
		$"A chart holds up to {IndicatorConstants.MaxIndicatorsPerChart} indicators. Remove one to add another.";

	/// <summary>The folder of indicator scripts (opened from the picker).</summary>
	public string IndicatorScriptsDirectory => _indicatorManager.GetScriptsDirectory();

	partial void OnIndicatorSearchChanged(string? value) => RefreshIndicatorChoices();

	// A different series (contract, timeframe): the old values don't line up with its bars
	partial void OnBarsChanged(IReadOnlyList<BarDto> oldValue, IReadOnlyList<BarDto> newValue)
	{
		if (!IsContinuation(oldValue, newValue))
			IndicatorResults = [.. IndicatorResults.Select(IndicatorChartHelpers.WithoutPlots)];
		ScheduleIndicators();
		RefreshTools();
	}

	public void RefreshIndicatorChoices() =>
		IndicatorChoices = [
			.. _indicatorManager
				.GetDefinitions()
				.Where(x =>
					SettingsSearchHelpers.Matches(
						IndicatorSearch,
						x.Name,
						x.ShortName,
						x.FilePath is { } path ? Path.GetFileName(path) : null
					)
				)
				.Select(x => new IndicatorChoiceItem(x)),
		];

	/// <summary>
	/// Indicator manager: lists the chart's indicators with their legend labels (the definition's
	/// short name and inputs, or the file name while it is missing).
	/// </summary>
	public void RefreshChartIndicators()
	{
		ChartIndicators =
		[
			.. _indicators.Select(x => new ChartIndicatorItem(
				x.Id,
				_indicatorManager.GetDefinition(x.DefinitionId) is { } definition
					? IndicatorHelpers.GetLabel(definition, x.Inputs)
					: IndicatorHelpers.GetMissingLabel(x.DefinitionId),
				x.IsVisible
			)),
		];
		OnPropertyChanged(nameof(CanAddIndicator));
	}

	/// <summary>Picker: adds the indicator with its defaults.</summary>
	public void AddIndicator(string definitionId)
	{
		if (!CanAddIndicator)
			return;
		try
		{
			_indicators = [.. _indicators, _indicatorManager.CreateInstance(definitionId)];
		}
		catch (LSCoreBadRequestException ex)
		{
			ErrorMessage = ex.Message;
			return;
		}
		OnIndicatorsEdited();
	}

	/// <summary>Legend: shows or hides an indicator (it stays in the legend).</summary>
	public void ToggleIndicator(string instanceId)
	{
		var instance = _indicators.FirstOrDefault(x => x.Id == instanceId);
		if (instance == null)
			return;
		var toggled = new IndicatorInstanceDto
		{
			Id = instance.Id,
			DefinitionId = instance.DefinitionId,
			Inputs = instance.Inputs,
			IsVisible = !instance.IsVisible,
			PlotColors = instance.PlotColors,
		};
		_indicators = [.. _indicators.Select(x => x.Id == instanceId ? toggled : x)];
		// Hiding shows at once; showing again once computed
		foreach (var result in IndicatorResults.Where(x => x.InstanceId == instanceId))
			result.IsVisible = toggled.IsVisible;
		IndicatorResults = [.. IndicatorResults];
		OnIndicatorsEdited();
	}

	/// <summary>Legend and indicator manager: removes an indicator from this chart.</summary>
	public void RemoveIndicator(string instanceId)
	{
		_indicators = [.. _indicators.Where(x => x.Id != instanceId)];
		IndicatorResults = [.. IndicatorResults.Where(x => x.InstanceId != instanceId)];
		OnIndicatorsEdited();
	}

	/// <summary>Settings dialog: the indicator's definition (null when it is gone) and instance.</summary>
	public (IndicatorDefinitionDto? Definition, IndicatorInstanceDto? Instance) GetIndicator(
		string instanceId
	)
	{
		var instance = _indicators.FirstOrDefault(x => x.Id == instanceId);
		return (
			instance != null ? _indicatorManager.GetDefinition(instance.DefinitionId) : null,
			instance
		);
	}

	/// <summary>Settings dialog: new input values; throws the validation error to show.</summary>
	public void UpdateIndicatorInputs(string instanceId, Dictionary<string, double> inputs)
	{
		var (definition, instance) = GetIndicator(instanceId);
		if (definition == null || instance == null)
			return;
		var updated = _indicatorManager.UpdateInputs(
			new UpdateIndicatorInputsRequest
			{
				Definition = definition,
				Instance = instance,
				Inputs = inputs,
			}
		);
		_indicators = [.. _indicators.Select(x => x.Id == instanceId ? updated : x)];
		OnIndicatorsEdited();
	}

	private void OnIndicatorsEdited()
	{
		RefreshChartIndicators();
		ScheduleIndicators();
		OnTemplateSettingsEdited();
	}

	// From the indicator manager's threads (a script compiled or was removed)
	private void OnIndicatorDefinitionsChanged(IReadOnlyCollection<string> definitionIds) =>
		Dispatcher.UIThread.Post(() =>
		{
			if (_disposed)
				return;
			RefreshIndicatorChoices();
			if (_indicators.Any(x => definitionIds.Contains(x.DefinitionId)))
			{
				RefreshChartIndicators();
				ScheduleIndicators();
			}
		});

	// Throttled: the first change starts the timer, everything until it fires is one compute
	private void ScheduleIndicators()
	{
		if (_disposed)
			return;
		if (_indicators.Count == 0)
		{
			_computeTimer.Stop();
			IndicatorResults = [];
			return;
		}
		if (!_computeTimer.IsEnabled)
			_computeTimer.Start();
	}

	// One compute at a time on the thread pool; a change while it runs computes again after it
	private void ComputeIndicators()
	{
		_computeTimer.Stop();
		if (_disposed)
			return;
		if (_computing)
		{
			_computePending = true;
			return;
		}
		var request = new ComputeIndicatorsRequest
		{
			Bars = Bars,
			Indicators = _indicators,
			IsIntraday = IsIntraday,
		};
		_computing = true;
		_ = Task.Run(() => _indicatorManager.Compute(request))
			.ContinueWith(
				task => Dispatcher.UIThread.Post(() => OnIndicatorsComputed(request, task)),
				TaskScheduler.Default
			);
	}

	private void OnIndicatorsComputed(
		ComputeIndicatorsRequest request,
		Task<IReadOnlyList<IndicatorResultDto>> task
	)
	{
		_computing = false;
		if (_disposed)
			return;
		var current = ReferenceEquals(request.Indicators, _indicators);
		if (task.IsCompletedSuccessfully)
		{
			if (current && IsContinuation(request.Bars, Bars))
				IndicatorResults = task.Result;
		}
		else if (task.Exception?.GetBaseException() is { } ex)
			_messageLogManager.Add(
				MessageSeverity.Warning,
				MessageLogConstants.SourceIndicators,
				$"{Title}: indicators failed ({ErrorMessageHelpers.ToUserMessage(ex)})"
			);
		if (_computePending || !current || !ReferenceEquals(request.Bars, Bars))
		{
			_computePending = false;
			ScheduleIndicators();
		}
	}

	// The same series, possibly with bars added or the last one updated
	private static bool IsContinuation(
		IReadOnlyList<BarDto> previous,
		IReadOnlyList<BarDto> next
	) =>
		previous.Count == 0
			? next.Count == 0
			: next.Count >= previous.Count && previous[0].Time == next[0].Time;
}
