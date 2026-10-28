using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Charts;

namespace LT.App.Widgets.Chart;

/// <summary>
/// Footprint bars (<see cref="ChartBarStyle.Footprint"/>, toggled from the header, saved per
/// chart): once the bars are in, live trades with their side
/// (<see cref="IFootprintManager.SubscribeTradesAsync"/>, two of IB's five tick-by-tick
/// subscriptions) and past trades paged back from now, newest bars first, through the session's
/// first bar or <see cref="FootprintConstants.MaxBackfillRequests"/> requests
/// (<see cref="ChartFootprint"/>). Bars waiting for their trades draw as candles marked loading,
/// bars past the backfill or without a live stream as candles marked "no footprint". The status
/// line says how far the footprint reaches and why it stops.
/// </summary>
public partial class ChartViewModel
{
	private readonly IFootprintManager _footprintManager;
	private CancellationTokenSource? _footprintLoad;

	// Why the footprint is short of live trades or quotes; shown in the status line
	private string? _footprintLiveError;
	private string? _footprintQuotesError;
	private string? _footprintUnavailable;

	/// <summary>How the bars are drawn: candles, or footprints on intraday charts.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(BarStyleLabel), nameof(BarStyleToolTip))]
	public partial ChartBarStyle BarStyle { get; set; }

	/// <summary>The current load's footprints (null when candles or not loaded); redraw on <see cref="FootprintVersion"/>.</summary>
	[ObservableProperty]
	public partial ChartFootprint? Footprint { get; set; }

	/// <summary>Bumped whenever <see cref="Footprint"/> changed inside, so the chart redraws.</summary>
	[ObservableProperty]
	public partial int FootprintVersion { get; set; }

	/// <summary>Diagonal imbalance ratio (Settings → Charts → Footprint, 3 = 300 %).</summary>
	[ObservableProperty]
	public partial double FootprintImbalanceRatio { get; set; } =
		FootprintConstants.DefaultImbalancePercent / 100.0;

	/// <summary>Header bar style toggle.</summary>
	public string BarStyleLabel => BarStyle == ChartBarStyle.Footprint ? "Footprint" : "Candles";

	public string BarStyleToolTip =>
		BarStyle == ChartBarStyle.Footprint
			? "Footprint: volume at the bid × at the ask per price, imbalances, the bar's busiest price boxed. Zoom in for the numbers. Click for candles"
			: "Candles. Click for footprint bars (intraday: volume at the bid and the ask per price; uses two of IB's five tick-by-tick subscriptions)";

	/// <summary>Header bar style toggle: candles ↔ footprint, for this chart only.</summary>
	[RelayCommand]
	public void ToggleBarStyle() =>
		BarStyle =
			BarStyle == ChartBarStyle.Footprint ? ChartBarStyle.Candles : ChartBarStyle.Footprint;

	partial void OnBarStyleChanged(ChartBarStyle value)
	{
		if (value == ChartBarStyle.Footprint)
			StartFootprint();
		else
			StopFootprint();
		_status.Rebuild();
	}

	// The current load's footprints, when its bars are in (a load still running starts them itself)
	private void StartFootprint()
	{
		if (IsLoading || _loaded == null || _load is not { } load || _contract is not { } contract)
			return;
		_ = LoadFootprintAsync(
			new GetBarsRequest
			{
				Contract = contract,
				Timeframe = SelectedTimeframe.Value,
				UseRegularTradingHours = UseRegularTradingHours,
			},
			load.Token
		);
	}

	private void StopFootprint()
	{
		_footprintLoad?.Cancel();
		_footprintLoad?.Dispose();
		_footprintLoad = null;
		Footprint?.Dispose();
		Footprint = null;
		_footprintLiveError = null;
		_footprintQuotesError = null;
		_footprintUnavailable = null;
	}

	// On the UI thread, after the bars loaded: the live trades on the thread pool, the backfill
	// here (each page's continuation comes back on the UI thread)
	private async Task LoadFootprintAsync(GetBarsRequest request, CancellationToken loadToken)
	{
		StopFootprint();
		if (BarStyle != ChartBarStyle.Footprint || loadToken.IsCancellationRequested)
			return;
		var contract = request.Contract;
		_footprintUnavailable =
			!ChartTimeframeHelpers.IsIntraday(request.Timeframe) ? "intraday timeframes only"
			: contract.SecurityType == MarketDataConstants.ForexType ? "forex has no trades"
			: !IsConnected ? "not connected"
			: Bars.Count == 0 ? "no bars"
			: null;
		if (_footprintUnavailable != null)
		{
			_status.Rebuild();
			return;
		}

		var cancellation = CancellationTokenSource.CreateLinkedTokenSource(loadToken);
		_footprintLoad = cancellation;
		var cancellationToken = cancellation.Token;
		double tickSize;
		try
		{
			tickSize = await _footprintManager.GetTickSizeAsync(contract, cancellationToken);
		}
		catch (Exception) when (cancellationToken.IsCancellationRequested)
		{
			return;
		}
		catch (Exception)
		{
			tickSize = FootprintHelpers.GetTickSize(0);
		}
		if (cancellationToken.IsCancellationRequested)
			return;

		var now = _timeProvider.GetUtcNow();
		// IB stamps trades in whole seconds; the past pages end strictly before this one
		var liveFrom = new DateTimeOffset(
			now.UtcTicks - now.UtcTicks % TimeSpan.TicksPerSecond,
			TimeSpan.Zero
		);
		ChartFootprint? footprint = null;
		footprint = new ChartFootprint(
			request.Timeframe,
			tickSize,
			liveFrom,
			FootprintHelpers.GetSessionStart(Bars, DisplayTime.Zone) ?? liveFrom,
			_timeProvider,
			() =>
			{
				if (Footprint != footprint)
					return;
				FootprintVersion++;
				_status.Rebuild();
			}
		);
		Footprint = footprint;
		_status.Rebuild();
		_ = Task.Run(
			() => StreamFootprintTradesAsync(contract, footprint, cancellationToken),
			CancellationToken.None
		);
		await BackfillFootprintAsync(request, footprint, cancellationToken);
	}

	// Pages of past trades, newest first, until the session's first bar or the request cap
	private async Task BackfillFootprintAsync(
		GetBarsRequest request,
		ChartFootprint footprint,
		CancellationToken cancellationToken
	)
	{
		while (footprint.GetNextPageEnd(FootprintConstants.MaxBackfillRequests) is { } before)
		{
			try
			{
				var page = await _footprintManager.GetTradesAsync(
					new GetFootprintTradesRequest
					{
						Contract = request.Contract,
						UseRegularTradingHours = request.UseRegularTradingHours,
						Before = before,
					},
					cancellationToken
				);
				if (cancellationToken.IsCancellationRequested)
					return;
				footprint.AddPage(page, _live?.Bars ?? Bars);
			}
			catch (Exception) when (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch (Exception ex)
			{
				_messageLogManager.Add(
					MessageSeverity.Info,
					MessageLogConstants.SourceChart,
					$"{ContractHelpers.GetDisplayName(request.Contract)}: footprint history unavailable ({ErrorMessageHelpers.ToUserMessage(ex)})"
				);
				break;
			}
		}
		footprint.StopBackfill();
	}

	// Thread pool: live trades into the footprint; a refused or ended stream leaves newer bars
	// without their footprint and says why
	private async Task StreamFootprintTradesAsync(
		ContractDto contract,
		ChartFootprint footprint,
		CancellationToken cancellationToken
	)
	{
		string? error = null;
		try
		{
			await foreach (
				var trade in _footprintManager
					.SubscribeTradesAsync(
						contract,
						ex =>
						{
							var message = ErrorMessageHelpers.ToUserMessage(ex);
							_messageLogManager.Add(
								MessageSeverity.Info,
								MessageLogConstants.SourceChart,
								$"{ContractHelpers.GetDisplayName(contract)}: footprint bid / ask unavailable, sides by the tick rule ({message})"
							);
							Dispatcher.UIThread.Post(() =>
							{
								if (Footprint != footprint)
									return;
								_footprintQuotesError = message;
								_status.Rebuild();
							});
						},
						cancellationToken
					)
					.ConfigureAwait(false)
			)
				footprint.AddLiveTrade(trade, () => _live?.Bars ?? Bars);
			error = "live trades ended";
		}
		catch (Exception) when (cancellationToken.IsCancellationRequested)
		{
			return;
		}
		catch (Exception ex)
		{
			error = ErrorMessageHelpers.ToUserMessage(ex);
			_messageLogManager.Add(
				MessageSeverity.Info,
				MessageLogConstants.SourceChart,
				$"{ContractHelpers.GetDisplayName(contract)}: footprint live trades unavailable, newer bars show as candles ({error})"
			);
		}
		if (cancellationToken.IsCancellationRequested)
			return;
		var failedAt = _timeProvider.GetUtcNow();
		Dispatcher.UIThread.Post(() =>
		{
			if (Footprint != footprint)
				return;
			footprint.SetLiveFailed(failedAt);
			_footprintLiveError = error;
			_status.Rebuild();
		});
	}

	// What the status line says about the footprint: how far back it reaches and why it stops
	private string GetFootprintStatus()
	{
		if (BarStyle != ChartBarStyle.Footprint)
			return string.Empty;
		if (_footprintUnavailable != null)
			return $" · footprint: {_footprintUnavailable}";
		if (Footprint is not { } footprint)
			return string.Empty;
		var text =
			footprint.IsBackfilling
				? $" · footprint loading {footprint.Requests}/{FootprintConstants.MaxBackfillRequests}"
			: footprint.PagedTo > footprint.BackfillTo
				? $" · footprint from {DisplayTime.FormatDayTime(footprint.PagedTo)}"
			: " · footprint";
		if (_footprintLiveError != null)
			text += $" (not live: {_footprintLiveError})";
		else if (_footprintQuotesError != null)
			text += " (sides by tick rule)";
		return text;
	}

	private void ApplyFootprintSettings(int imbalancePercent) =>
		FootprintImbalanceRatio =
			Math.Clamp(
				imbalancePercent,
				FootprintConstants.ImbalancePercentMin,
				FootprintConstants.ImbalancePercentMax
			) / 100.0;
}
