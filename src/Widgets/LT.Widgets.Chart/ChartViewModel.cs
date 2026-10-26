using System.Collections.ObjectModel;
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
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Charts;
using LT.Contracts.Requests.Contracts;

namespace LT.App.Widgets.Chart;

/// <summary>
/// One chart. Several can be open; each has its own timeframe and either follows the selected
/// contract (linked) or stays on the contract it shows (pinned).
/// </summary>
public partial class ChartViewModel : ConnectedViewModelBase, IWidgetState, ILinkable
{
	private readonly IChartManager _chartManager;
	private readonly IContractManager _contractManager;
	private readonly IMessageLogManager _messageLogManager;
	private CancellationTokenSource? _load;
	private ContractDto? _contract;

	public ChartViewModel(
		IChartManager chartManager,
		IContractManager contractManager,
		IConnectionManager connectionManager,
		IMessageLogManager messageLogManager,
		ISettingsManager settingsManager,
		LinkGroupStore linkGroupStore
	)
		: base(connectionManager)
	{
		_chartManager = chartManager;
		_contractManager = contractManager;
		_messageLogManager = messageLogManager;
		Link = new ContractLink(linkGroupStore, () => _contract);
		Link.ContractReceived += OnContractReceived;

		Timeframes = Enum.GetValues<ChartTimeframe>().Select(x => new TimeframeOption(x)).ToList();
		var saved = settingsManager.GetLayout().ChartTimeframe;
		SelectedTimeframe = Timeframes.FirstOrDefault(x => x.Value == saved) ?? Timeframes[1];

		_contract = linkGroupStore.Get(Link.Group);
		Reload();
	}

	public ContractLink Link { get; }

	/// <summary>Ticker box suggestions for an empty box: the recent contracts.</summary>
	public IReadOnlyList<ContractSuggestionItem> GetRecentSuggestions() =>
		_contractManager.GetRecentContracts().Select(x => new ContractSuggestionItem(x)).ToList();

	/// <summary>Ticker box suggestions for typed text: IB's listings of the symbol.</summary>
	public Task<IEnumerable<object>> GetTickerSuggestionsAsync(
		string? text,
		CancellationToken cancellationToken
	) =>
		string.IsNullOrWhiteSpace(text)
			? Task.FromResult<IEnumerable<object>>(GetRecentSuggestions())
		: IsConnected
			? ContractSuggestionItem.SearchAsync(_contractManager, text, cancellationToken)
		: Task.FromResult(Enumerable.Empty<object>());

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
		if (placement.ChartContract != null)
			ShowContract(placement.ChartContract);
	}

	public void SaveState(WidgetPlacementDto placement)
	{
		placement.ChartTimeframe = SelectedTimeframe.Value;
		placement.ChartLinked = Link.Group != LinkGroup.None;
		placement.ChartContract = _contract;
	}

	public override void Dispose()
	{
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

	protected override void OnConnected(CancellationToken session) => Reload();

	protected override void OnDisconnected() => _load?.Cancel();

	[RelayCommand]
	public void Reload()
	{
		_load?.Cancel();
		_load?.Dispose();
		_load = new CancellationTokenSource();
		_ = LoadAsync(_load.Token);
	}

	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		var contract = _contract;
		ErrorMessage = null;
		StatusText = null;
		if (contract == null)
		{
			Title = "Chart";
			Bars = [];
			StatusText = "Select a contract in the watchlist or positions.";
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
			StatusText = result.IsStale
				? $"Cached {result.FetchedAt.ToLocalTime():HH:mm} — {result.Error}"
				: $"Loaded {result.FetchedAt.ToLocalTime():HH:mm:ss}";
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
				StatusText = $"Live · {update.Time.ToLocalTime():HH:mm:ss}";
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			var message = ErrorMessageHelpers.ToUserMessage(ex);
			StatusText = $"{StatusText} · real-time updates unavailable";
			_messageLogManager.Add(
				MessageSeverity.Info,
				MessageLogConstants.SourceChart,
				$"{ContractHelpers.GetDisplayName(contract)}: real-time bars unavailable ({message})"
			);
		}
	}
}
