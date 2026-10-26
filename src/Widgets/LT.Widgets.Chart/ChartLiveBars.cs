using System.Collections.Concurrent;
using Avalonia.Threading;
using LT.App.Helpers;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

/// <summary>
/// A chart's bars while they follow the market (one load of one series). 5-second bars
/// (<see cref="AddRealtimeBar"/>, UI thread) and live prices (<see cref="AddPrice"/>, any thread)
/// are folded into a working copy on the UI thread; <paramref name="publish"/> hands it to the
/// chart, at most once per redraw interval (<see cref="RedrawThrottle"/>, zero = every update).
/// <para>
/// Prices are queued and folded one burst per UI post, so every trade moves the bar without a
/// post per tick. They count only while 5-second bars are flowing
/// (<see cref="ChartLiveUpdateHelpers.ShouldApplyPrice"/>), and a 5-second bar older than the
/// newest price keeps the price's close (<see cref="ChartLiveUpdateHelpers.KeepsClose"/>).
/// </para>
/// </summary>
public sealed class ChartLiveBars : IDisposable
{
	private readonly ChartTimeframe _timeframe;
	private readonly TimeProvider _timeProvider;
	private readonly Action<Action> _post;
	private readonly RedrawThrottle _throttle;
	private readonly ConcurrentQueue<ChartPriceDto> _prices = new();
	private int _pricesQueued;
	private DateTimeOffset? _lastRealtimeBarAt;
	private DateTimeOffset? _lastPriceAt;
	private bool _disposed;

	public ChartLiveBars(
		IReadOnlyList<BarDto> bars,
		ChartTimeframe timeframe,
		TimeProvider timeProvider,
		TimeSpan redrawInterval,
		Action<IReadOnlyList<BarDto>> publish,
		Action<Action>? post = null
	)
	{
		Bars = bars;
		_timeframe = timeframe;
		_timeProvider = timeProvider;
		_post = post ?? (x => Dispatcher.UIThread.Post(x));
		_throttle = new RedrawThrottle(
			timeProvider,
			redrawInterval,
			() =>
			{
				if (!_disposed)
					publish(Bars);
			},
			_post
		);
	}

	/// <summary>The working bars, newest updates included (published or not yet).</summary>
	public IReadOnlyList<BarDto> Bars { get; private set; }

	/// <summary>A 5-second real-time bar; on the UI thread.</summary>
	public void AddRealtimeBar(BarDto bar)
	{
		if (_disposed)
			return;
		_lastRealtimeBarAt = _timeProvider.GetUtcNow();
		Bars = BarHelpers.Merge(
			Bars,
			bar,
			_timeframe,
			ChartLiveUpdateHelpers.KeepsClose(_lastPriceAt, bar.Time)
		);
		_throttle.Request();
	}

	/// <summary>A live price (last trade, forex midpoint); from any thread.</summary>
	public void AddPrice(ChartPriceDto price)
	{
		if (_disposed)
			return;
		_prices.Enqueue(price);
		if (Interlocked.Exchange(ref _pricesQueued, 1) == 0)
			_post(FoldPrices);
	}

	public void Dispose()
	{
		_disposed = true;
		_throttle.Dispose();
	}

	private void FoldPrices()
	{
		Interlocked.Exchange(ref _pricesQueued, 0);
		if (_disposed)
			return;
		var bars = Bars;
		// On the app's clock like the 5-second bars' arrival (tick-by-tick prices carry IB's time)
		var apply = ChartLiveUpdateHelpers.ShouldApplyPrice(
			_timeProvider.GetUtcNow(),
			_lastRealtimeBarAt
		);
		while (_prices.TryDequeue(out var price))
		{
			if (!apply)
				continue;
			bars = BarHelpers.MergePrice(bars, price.Price, price.Time, _timeframe);
			_lastPriceAt = price.Time;
		}
		if (ReferenceEquals(bars, Bars))
			return;
		Bars = bars;
		_throttle.Request();
	}
}
