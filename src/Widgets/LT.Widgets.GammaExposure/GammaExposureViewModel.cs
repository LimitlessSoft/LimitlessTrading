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
using LT.Contracts.Dtos.GammaExposure;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.GammaExposure;

namespace LT.App.Widgets.GammaExposure;

/// <summary>
/// Gamma exposure by strike of one expiration of the link group's underlying (or one typed into
/// the header title): a bar per strike around the spot, the spot price and the gamma flip marked.
/// Read when shown and every <see cref="GammaExposureConstants.RefreshInterval"/> after, or on
/// Refresh; each reading takes the options' lines only for as long as IB needs to answer
/// (<see cref="IGammaExposureManager"/>), and a hidden widget reads nothing.
/// </summary>
public partial class GammaExposureViewModel : ConnectedViewModelBase, ILinkable, ITickerTitled
{
	private readonly IGammaExposureManager _gammaExposureManager;
	private readonly IOptionChainManager _optionChainManager;
	private readonly IContractManager _contractManager;
	private readonly DisplayTimeStore _displayTime;
	private ContractDto? _underlying;
	private CancellationTokenSource? _load;
	private CancellationTokenSource? _reading;

	public GammaExposureViewModel(
		IGammaExposureManager gammaExposureManager,
		IOptionChainManager optionChainManager,
		IContractManager contractManager,
		IConnectionManager connectionManager,
		LinkGroupStore linkGroupStore,
		DisplayTimeStore displayTime
	)
		: base(connectionManager)
	{
		_gammaExposureManager = gammaExposureManager;
		_optionChainManager = optionChainManager;
		_contractManager = contractManager;
		_displayTime = displayTime;
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
	}

	/// <summary>The underlying comes from the link group (watchlist, chart, option chain …).</summary>
	public ContractLink Link { get; }

	/// <summary>The header title's ticker box: typing an underlying there reads its exposure.</summary>
	public TickerTitleEditor TickerEditor { get; }

	public ObservableCollection<ExpirationOption> Expirations { get; } = [];

	/// <summary>Frame header title: the underlying's ticker.</summary>
	[ObservableProperty]
	public partial string Title { get; set; } = "Gamma exposure";

	[ObservableProperty]
	public partial ExpirationOption? SelectedExpiration { get; set; }

	/// <summary>The last reading of the selected expiration; null before the first.</summary>
	[ObservableProperty]
	public partial GammaExposureDto? Exposure { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
	public partial bool IsReading { get; set; }

	/// <summary>"Reading options 24 / 82" while a reading runs.</summary>
	[ObservableProperty]
	public partial string? ProgressText { get; set; }

	[ObservableProperty]
	public partial string? InfoText { get; set; }

	[ObservableProperty]
	public partial string? NetText { get; set; }

	[ObservableProperty]
	public partial bool IsNetPositive { get; set; }

	[ObservableProperty]
	public partial string? CallsText { get; set; }

	[ObservableProperty]
	public partial string? PutsText { get; set; }

	[ObservableProperty]
	public partial string? SpotText { get; set; }

	[ObservableProperty]
	public partial string? FlipText { get; set; }

	/// <summary>"80 / 82 options · 13:42:05": how much of the reading counted, and when.</summary>
	[ObservableProperty]
	public partial string? CoverageText { get; set; }

	partial void OnSelectedExpirationChanged(ExpirationOption? value)
	{
		Exposure = null;
		StartReading();
	}

	partial void OnExposureChanged(GammaExposureDto? value) => ShowExposure(value);

	private bool CanRefresh() => !IsReading;

	/// <summary>Reads again now (and every refresh interval from here).</summary>
	[RelayCommand(CanExecute = nameof(CanRefresh))]
	private void Refresh() => StartReading();

	protected override void OnConnected(CancellationToken session) => _ = LoadExpirationsAsync();

	protected override void OnDisconnected()
	{
		CancelLoad();
		StopReading();
	}

	// Shown (and connected): read now and on every refresh interval
	protected override void OnStreamsStarted(CancellationToken streams) => StartReading();

	// Hidden: no reading runs; the last one stays on screen
	protected override void OnStreamsStopped() => StopReading();

	public override void Dispose()
	{
		Link.Dispose();
		CancelLoad();
		StopReading();
		base.Dispose();
	}

	/// <summary>Ticker box: resolves the ticker and reads its exposure.</summary>
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

	// A ticker typed here moves every widget of the link group along
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

	// New underlying or connection: list its expirations and pick the nearest
	private async Task LoadExpirationsAsync()
	{
		CancelLoad();
		StopReading();
		var load = (_load = new CancellationTokenSource()).Token;
		SelectedExpiration = null;
		Expirations.Clear();
		Exposure = null;
		ErrorMessage = null;

		var underlying = _underlying;
		Title = underlying == null ? "Gamma exposure" : ContractHelpers.GetDisplayName(underlying);
		if (underlying == null)
		{
			InfoText = "Pick a stock or index in a widget of the same link group.";
			return;
		}
		if (!ContractHelpers.SupportsOptionChain(underlying))
		{
			InfoText = $"{ContractHelpers.GetDisplayName(underlying)} has no options.";
			return;
		}
		InfoText = null;
		if (!IsConnected)
			return;

		try
		{
			var expirations = await _optionChainManager.GetExpirationsAsync(underlying, load);
			if (load.IsCancellationRequested)
				return;
			var today = _displayTime.Now.Date;
			foreach (var expiration in expirations)
				Expirations.Add(new ExpirationOption(expiration, today));
			SelectedExpiration = Expirations.FirstOrDefault();
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	// Runs while shown and connected with an expiration picked: now, then every interval
	private void StartReading()
	{
		StopReading();
		if (!IsStreaming || _underlying is not { } underlying || SelectedExpiration is not { } tab)
			return;
		_reading = CancellationTokenSource.CreateLinkedTokenSource(StreamsToken);
		var token = _reading.Token;
		var request = new GetGammaExposureRequest
		{
			Underlying = underlying,
			Expiration = tab.Expiration.Expiration,
			TradingClass = tab.Expiration.TradingClass,
		};
		_ = RunPeriodicAsync(
			ct => ReadAsync(request, ct),
			GammaExposureConstants.RefreshInterval,
			token
		);
	}

	private async Task ReadAsync(
		GetGammaExposureRequest request,
		CancellationToken cancellationToken
	)
	{
		IsReading = true;
		ProgressText = "Reading the chain…";
		try
		{
			var exposure = await _gammaExposureManager.GetGammaExposureAsync(
				request,
				new UiProgress(this, cancellationToken),
				cancellationToken
			);
			if (!cancellationToken.IsCancellationRequested)
				Exposure = exposure;
		}
		finally
		{
			if (!cancellationToken.IsCancellationRequested)
			{
				IsReading = false;
				ProgressText = null;
			}
		}
	}

	private void StopReading()
	{
		_reading?.Cancel();
		_reading?.Dispose();
		_reading = null;
		IsReading = false;
		ProgressText = null;
	}

	private void CancelLoad()
	{
		_load?.Cancel();
		_load?.Dispose();
		_load = null;
	}

	// The summary line follows Exposure (null clears it)
	private void ShowExposure(GammaExposureDto? exposure)
	{
		NetText =
			exposure == null ? null : GammaExposureHelpers.FormatExposure(exposure.NetExposure);
		IsNetPositive = exposure?.NetExposure >= 0;
		CallsText =
			exposure == null ? null : GammaExposureHelpers.FormatExposure(exposure.CallExposure);
		PutsText =
			exposure == null ? null : GammaExposureHelpers.FormatExposure(exposure.PutExposure);
		SpotText = exposure == null ? null : FormatHelpers.Price(exposure.SpotPrice);
		FlipText =
			exposure == null ? null
			: exposure.GammaFlip is { } flip ? FormatHelpers.Price(flip)
			: "—";
		CoverageText =
			exposure == null
				? null
				: $"{exposure.OptionsReceived} / {exposure.OptionsRequested} options · {_displayTime.Format(exposure.ReadAt)}";
	}

	// Reports come from the thread pool: the count reaches the screen on the UI thread
	private sealed class UiProgress(GammaExposureViewModel owner, CancellationToken reading)
		: IProgress<GammaExposureProgressDto>
	{
		public void Report(GammaExposureProgressDto value) =>
			Dispatcher.UIThread.Post(() =>
			{
				// A count posted just before the reading ended must not outlive it
				if (!reading.IsCancellationRequested && owner.IsReading)
					owner.ProgressText = $"Reading options {value.Done} / {value.Total}";
			});
	}
}
