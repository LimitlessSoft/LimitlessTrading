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
/// One chart. Several can be open; each has its own timeframe and either follows the selected
/// contract (linked) or stays on the contract it shows (pinned). The selected account's recorded
/// fills of the contract are drawn on it (trade journal: socket executions and IB Flex), closed
/// quantity joined entry → exit, and the break-even of an open position (live positions only).
/// <para>
/// Its indicators (saved with the chart) are computed on the thread pool by
/// <see cref="IIndicatorManager"/> whenever the bars, the indicator list or a script they use
/// change — at most every <see cref="IndicatorConstants.ComputeDebounce"/>, one compute at a
/// time — and posted back to the UI thread.
/// </para>
/// <para>
/// Its setup (timeframe, indicators, right margin) can be saved as a named template and applied
/// from the header, and can follow its template (changes saved to it and taken from it); see
/// ChartViewModel.Templates.cs.
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
		_indicatorManager = indicatorManager;
		_chartTemplateManager = chartTemplateManager;
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
		// Layouts saved before link groups: a chart pinned to its own ticker is not linked
		if (!placement.ChartLinked)
			Link.Group = LinkGroup.None;
		RightMargin = Math.Max(0, placement.ChartRightMargin ?? 0);
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
		placement.ChartLinked = Link.Group != LinkGroup.None;
		placement.ChartContract = _contract;
		placement.ChartRightMargin = RightMargin > 0 ? RightMargin : null;
		placement.ChartIndicators = _indicators.Count > 0 ? [.. _indicators] : null;
		placement.ChartTemplateId = _templateId;
		placement.ChartFollowTemplate = FollowTemplate;
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

	partial void OnSelectedTimeframeChanged(TimeframeOption value)
	{
		Reload();
		OnTemplateSettingsEdited();
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
