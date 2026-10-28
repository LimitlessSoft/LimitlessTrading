using System.Collections.Concurrent;
using Avalonia.Threading;
using LT.App.Helpers;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

/// <summary>
/// The footprint bars of one chart load (<see cref="ChartBarStyle.Footprint"/>): live trades from
/// <see cref="LiveFrom"/> on, and past trades paged back from it — newest bars first, down to
/// <see cref="BackfillTo"/> (the session's first bar) or until the chart stops asking
/// (<see cref="StopBackfill"/>: the request cap, an error). Read and changed on the UI thread;
/// live trades come from any thread (<see cref="AddLiveTrade"/>) and are folded one burst per UI
/// post; <paramref name="changed"/> is raised at most once per
/// <see cref="MarketDataConstants.UiFlushInterval"/>.
/// <para>
/// A bar older than <see cref="LiveFrom"/> is counted once the pages reach a trade before its
/// start: then all its trades are in and the first one has a trade before it for the tick rule
/// (past trades have no quotes; live ones go by the quote rule). Until then it is
/// <see cref="FootprintBarState.Loading"/>; past what the backfill reached,
/// <see cref="FootprintBarState.Unavailable"/>. Live trades of the bar forming at
/// <see cref="LiveFrom"/> are counted as they come and shown with its past ones.
/// </para>
/// </summary>
public sealed class ChartFootprint : IDisposable
{
	private readonly ChartTimeframe _timeframe;
	private readonly Action<Action> _post;
	private readonly RedrawThrottle _throttle;
	private readonly Dictionary<DateTimeOffset, FootprintBarDto> _bars = [];

	// Bars whose past trades are counted (or that start after LiveFrom: nothing past to count)
	private readonly HashSet<DateTimeOffset> _counted = [];
	private readonly ConcurrentQueue<FootprintTradeDto> _liveTrades = new();
	private int _liveQueued;

	// Every past trade fetched, oldest first; they cover [_pagedTo, LiveFrom)
	private List<TickByTickDto> _history = [];
	private DateTimeOffset _pagedTo;
	private bool _historyExhausted;
	private DateTimeOffset? _liveFailedAt;
	private bool _disposed;

	public ChartFootprint(
		ChartTimeframe timeframe,
		double tickSize,
		DateTimeOffset liveFrom,
		DateTimeOffset backfillTo,
		TimeProvider timeProvider,
		Action changed,
		Action<Action>? post = null
	)
	{
		_timeframe = timeframe;
		TickSize = tickSize;
		LiveFrom = liveFrom;
		BackfillTo = backfillTo;
		_pagedTo = liveFrom;
		_post = post ?? (x => Dispatcher.UIThread.Post(x));
		_throttle = new RedrawThrottle(
			timeProvider,
			MarketDataConstants.UiFlushInterval,
			() =>
			{
				if (!_disposed)
					changed();
			},
			_post
		);
	}

	public double TickSize { get; }

	/// <summary>Live trades count from here (IB time); past trades are paged back from here.</summary>
	public DateTimeOffset LiveFrom { get; }

	/// <summary>The backfill's target: bars from here on get their past trades.</summary>
	public DateTimeOffset BackfillTo { get; }

	/// <summary>Historical ticks requests sent so far.</summary>
	public int Requests { get; private set; }

	/// <summary>Past trades are still being paged back.</summary>
	public bool IsBackfilling { get; private set; } = true;

	/// <summary>The oldest time the past trades reach back to.</summary>
	public DateTimeOffset PagedTo => _pagedTo;

	/// <summary>
	/// Where the next page of past trades ends (it holds trades strictly before it), or null when
	/// the backfill is done: the target is reached, IB had no more, it was stopped, or
	/// <paramref name="maxRequests"/> were sent.
	/// </summary>
	public DateTimeOffset? GetNextPageEnd(int maxRequests) =>
		IsBackfilling && !_historyExhausted && _pagedTo > BackfillTo && Requests < maxRequests
			? _pagedTo
			: null;

	/// <summary>
	/// A page of past trades (oldest first) ending where <see cref="GetNextPageEnd"/> said; counts
	/// every bar of <paramref name="bars"/> the trades now cover. An empty page means IB has no
	/// older trades: the bars counted so far are all there is.
	/// </summary>
	public void AddPage(IReadOnlyList<TickByTickDto> page, IReadOnlyList<BarDto> bars)
	{
		if (_disposed)
			return;
		Requests++;
		var older = page.Where(x => x.Time < _pagedTo && x.Price > 0).ToList();
		if (older.Count == 0)
			_historyExhausted = true;
		else
		{
			older.AddRange(_history);
			_history = older;
			_pagedTo = _history[0].Time;
		}
		CountCoveredBars(bars);
		_throttle.Request();
	}

	/// <summary>No more pages (the cap was reached, a page failed, the chart left footprints).</summary>
	public void StopBackfill()
	{
		if (!IsBackfilling)
			return;
		IsBackfilling = false;
		_throttle.Request();
	}

	/// <summary>A live trade, classified; from any thread. Trades before <see cref="LiveFrom"/> are the backfill's.</summary>
	public void AddLiveTrade(FootprintTradeDto trade, Func<IReadOnlyList<BarDto>> bars)
	{
		if (_disposed || trade.Time < LiveFrom)
			return;
		_liveTrades.Enqueue(trade);
		if (Interlocked.Exchange(ref _liveQueued, 1) == 0)
			_post(() => FoldLiveTrades(bars()));
	}

	/// <summary>
	/// The live stream failed or ended (limit reached, IB refusal, disconnected) at
	/// <paramref name="at"/> (<see cref="LiveFrom"/> when it never started): bars still open then
	/// can't be complete.
	/// </summary>
	public void SetLiveFailed(DateTimeOffset at)
	{
		_liveFailedAt ??= at;
		_throttle.Request();
	}

	/// <summary>The bar's footprint once its trades are all counted; null otherwise.</summary>
	public FootprintBarDto? Get(DateTimeOffset barTime) =>
		GetState(barTime) == FootprintBarState.Ready ? _bars.GetValueOrDefault(barTime) : null;

	public FootprintBarState GetState(DateTimeOffset barTime)
	{
		// Live trades are missing from where the stream stopped (or never started)
		var end = barTime + ChartTimeframeHelpers.GetBarLength(_timeframe);
		if (end > LiveFrom && _liveFailedAt is { } failedAt && end > failedAt)
			return FootprintBarState.Unavailable;
		if (barTime >= LiveFrom || _counted.Contains(barTime))
			return FootprintBarState.Ready;
		return IsBackfilling && !_historyExhausted && barTime >= BackfillTo
			? FootprintBarState.Loading
			: FootprintBarState.Unavailable;
	}

	public void Dispose()
	{
		_disposed = true;
		_throttle.Dispose();
	}

	// Every bar the past trades now reach before (a trade older than its start is in), or every
	// bar left once IB has no more
	private void CountCoveredBars(IReadOnlyList<BarDto> bars)
	{
		var length = ChartTimeframeHelpers.GetBarLength(_timeframe);
		for (var i = bars.Count - 1; i >= 0; i--)
		{
			var bar = bars[i];
			if (bar.Time >= LiveFrom || _counted.Contains(bar.Time))
				continue;
			// Covered: a trade before its start is in. At the start of IB's data, the bar the
			// oldest trade falls in is all there is; older ones have nothing
			var covered = bar.Time > _pagedTo || _historyExhausted && bar.Time + length > _pagedTo;
			if (!covered)
				break;
			var first = LowerBound(_history, bar.Time);
			var last = LowerBound(_history, Min(bar.Time + length, LiveFrom));
			var previous = first > 0 ? _history[first - 1] : null;
			var footprint = GetOrAdd(bar.Time);
			foreach (
				var trade in FootprintHelpers.ClassifyByTickRule(
					_history.Skip(first).Take(last - first),
					previous
				)
			)
				FootprintHelpers.AddTrade(footprint, trade);
			_counted.Add(bar.Time);
		}
	}

	private void FoldLiveTrades(IReadOnlyList<BarDto> bars)
	{
		Interlocked.Exchange(ref _liveQueued, 0);
		if (_disposed)
			return;
		while (_liveTrades.TryDequeue(out var trade))
			if (FootprintHelpers.GetBarTime(bars, trade.Time, _timeframe) is { } barTime)
				FootprintHelpers.AddTrade(GetOrAdd(barTime), trade);
		_throttle.Request();
	}

	private FootprintBarDto GetOrAdd(DateTimeOffset barTime)
	{
		if (!_bars.TryGetValue(barTime, out var bar))
			_bars[barTime] = bar = new FootprintBarDto { Time = barTime, TickSize = TickSize };
		return bar;
	}

	// First trade at or after time
	private static int LowerBound(List<TickByTickDto> trades, DateTimeOffset time)
	{
		var low = 0;
		var high = trades.Count;
		while (low < high)
		{
			var middle = low + (high - low) / 2;
			if (trades[middle].Time < time)
				low = middle + 1;
			else
				high = middle;
		}
		return low;
	}

	private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
