using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

namespace LT.App.Widgets.Chart;

/// <summary>
/// One chart. Several can be open; each has its own timeframe and either follows the selected
/// contract (linked) or stays on the contract it shows (pinned).
/// </summary>
public partial class ChartViewModel : ConnectedViewModelBase, IWidgetState
{
	private readonly IChartManager _chartManager;
	private readonly IContractManager _contractManager;
	private readonly IMessageLogManager _messageLogManager;
	private readonly SelectedContractStore _selectedContractStore;
	private CancellationTokenSource? _load;
	private ContractDto? _contract;

	public ChartViewModel(
		IChartManager chartManager,
		IContractManager contractManager,
		IConnectionManager connectionManager,
		IMessageLogManager messageLogManager,
		ISettingsManager settingsManager,
		SelectedContractStore selectedContractStore
	)
		: base(connectionManager)
	{
		_chartManager = chartManager;
		_contractManager = contractManager;
		_messageLogManager = messageLogManager;
		_selectedContractStore = selectedContractStore;

		Timeframes = Enum.GetValues<ChartTimeframe>().Select(x => new TimeframeOption(x)).ToList();
		var saved = settingsManager.GetLayout().ChartTimeframe;
		SelectedTimeframe = Timeframes.FirstOrDefault(x => x.Value == saved) ?? Timeframes[1];

		_contract = selectedContractStore.SelectedContract;
		selectedContractStore.PropertyChanged += OnStoreChanged;
		Reload();
	}

	/// <summary>Recent tickers offered in the title's ticker box.</summary>
	public ObservableCollection<RecentContractItem> RecentContracts { get; } = [];

	public void RefreshRecentContracts()
	{
		RecentContracts.Clear();
		foreach (var contract in _contractManager.GetRecentContracts())
			RecentContracts.Add(new RecentContractItem(contract));
	}

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

	// A ticker picked for this chart stays on it, whatever is selected elsewhere
	private void ShowContract(ContractDto contract)
	{
		_contract = contract;
		IsLinked = false;
		Reload();
	}

	/// <summary>Follows the contract selected in the watchlist / positions; off = pinned.</summary>
	[ObservableProperty]
	public partial bool IsLinked { get; set; } = true;

	partial void OnIsLinkedChanged(bool value)
	{
		if (!value || _contract == _selectedContractStore.SelectedContract)
			return;
		_contract = _selectedContractStore.SelectedContract;
		Reload();
	}

	public void LoadState(WidgetPlacementDto placement)
	{
		if (placement.ChartTimeframe is { } timeframe)
			SelectedTimeframe =
				Timeframes.FirstOrDefault(x => x.Value == timeframe) ?? SelectedTimeframe;
		if (!placement.ChartLinked && placement.ChartContract != null)
		{
			_contract = placement.ChartContract;
			IsLinked = false;
			Reload();
		}
	}

	public void SaveState(WidgetPlacementDto placement)
	{
		placement.ChartTimeframe = SelectedTimeframe.Value;
		placement.ChartLinked = IsLinked;
		placement.ChartContract = IsLinked ? null : _contract;
	}

	public override void Dispose()
	{
		_selectedContractStore.PropertyChanged -= OnStoreChanged;
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

	private void OnStoreChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(SelectedContractStore.SelectedContract) || !IsLinked)
			return;
		_contract = _selectedContractStore.SelectedContract;
		Reload();
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
		Title =
			$"{ContractHelpers.GetDisplayName(contract)} · {ChartTimeframeHelpers.GetDisplayName(timeframe)}";
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
