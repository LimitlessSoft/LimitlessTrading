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
/// <see cref="LiveFrom"/> on, and past trades paged back bar by bar — each bar filled from its end
/// towards its start, so a bar shows the trades loaded so far at once. Pages go to the visible
/// bars first (newest first: one page each, then the rest of each), then back through the session
/// (<see cref="SessionStart"/>) in the background; scrolling back to older bars
/// (<see cref="SetVisible"/>) sends the pages there. At most <c>maxRequests</c> pages per load.
/// Read and changed on the UI thread; live trades come from any thread
/// (<see cref="AddLiveTrade"/>) and are folded one burst per UI post; <c>changed</c> is raised at
/// most once per <see cref="MarketDataConstants.UiFlushInterval"/>.
/// <para>
/// Before IB's pages, the trades the footprint trade cache holds of a bar's UTC day are counted
/// (<see cref="GetNextRecordedDay"/>, <see cref="AddRecorded"/>): pages go only where neither
/// brought them. Trades are counted into each bar's price levels as they arrive (no raw ticks are
/// kept); what has been counted is a set of stretches of time, each holding every trade in it.
/// Past trades have no quotes (cached live ones keep the quote rule's side), so their side goes
/// by the tick rule; the run of trades at the start of a stretch (until the first price change)
/// waits for the trade before it, counted half on each side meanwhile, and moves to its side once
/// the stretch before it is in. A bar is <see cref="FootprintBarState.Ready"/> once a stretch
/// covers it from a trade before its start,
/// <see cref="FootprintBarState.Partial"/> while it has some of its trades,
/// <see cref="FootprintBarState.Loading"/> before its first page and
/// <see cref="FootprintBarState.Unavailable"/> past what the backfill can reach.
/// </para>
/// </summary>
public sealed class ChartFootprint : IDisposable
{
	// Without a view yet (no render), the newest bars a chart shows at the footprint zoom
	private const int DefaultVisibleBars = 12;

	private readonly ChartTimeframe _timeframe;
	private readonly TimeSpan _length;
	private readonly Func<IReadOnlyList<BarDto>> _getBars;
	private readonly int _maxRequests;
	private readonly Action<Action> _post;
	private readonly RedrawThrottle _throttle;
	private readonly Dictionary<DateTimeOffset, FootprintBarDto> _bars = [];

	// The stretches of time before LiveFrom whose trades are all counted, oldest first, apart
	private readonly List<Span> _spans = [];

	// The UTC days whose cached trades are in (or were looked for)
	private readonly HashSet<DateTime> _recordedDays = [];
	private readonly ConcurrentQueue<FootprintTradeDto> _liveTrades = new();
	private int _liveQueued;

	// IB has no trades before this (an empty page)
	private DateTimeOffset _noTradesBefore = DateTimeOffset.MinValue;
	private DateTimeOffset? _visibleFrom;
	private DateTimeOffset? _visibleTo;
	private DateTimeOffset? _liveFailedAt;
	private bool _disposed;

	public ChartFootprint(
		ChartTimeframe timeframe,
		double tickSize,
		DateTimeOffset liveFrom,
		DateTimeOffset sessionStart,
		Func<IReadOnlyList<BarDto>> bars,
		TimeProvider timeProvider,
		Action changed,
		Action<Action>? post = null,
		int maxRequests = FootprintConstants.MaxBackfillRequests
	)
	{
		_timeframe = timeframe;
		_length = ChartTimeframeHelpers.GetBarLength(timeframe);
		_getBars = bars;
		_maxRequests = maxRequests;
		TickSize = tickSize;
		LiveFrom = liveFrom;
		SessionStart = sessionStart;
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

	/// <summary>The background backfill goes back to here (the session's first bar).</summary>
	public DateTimeOffset SessionStart { get; }

	/// <summary>Historical ticks requests sent so far.</summary>
	public int Requests { get; private set; }

	/// <summary>No more pages: the request cap was reached, a page failed or the chart left footprints.</summary>
	public bool IsStopped { get; private set; }

	/// <summary>
	/// The bars the chart shows (oldest and newest start): their pages go first, and the backfill
	/// reaches back to them when they are older than the session. True when that is new.
	/// </summary>
	public bool SetVisible(DateTimeOffset from, DateTimeOffset to)
	{
		if (_visibleFrom == from && _visibleTo == to)
			return false;
		_visibleFrom = from;
		_visibleTo = to;
		return true;
	}

	/// <summary>
	/// Where the next page of past trades ends (it holds trades strictly before it), or null when
	/// there is nothing more to load: the visible bars and the session back to
	/// <see cref="SessionStart"/> are complete, IB had no more, or the backfill was stopped.
	/// Visible bars without a page come first (newest first), then the rest of the visible bars,
	/// then the session, each bar paged back from where it got to.
	/// </summary>
	public DateTimeOffset? GetNextPageEnd()
	{
		if (_disposed || IsStopped)
			return null;
		if (Requests >= _maxRequests)
		{
			IsStopped = true;
			_throttle.Request();
			return null;
		}
		var bars = _getBars();
		var last = bars.Count - 1;
		while (last >= 0 && bars[last].Time >= LiveFrom)
			last--;
		if (last < 0)
			return null;
		var (visibleFirst, visibleLast) = GetVisibleRange(bars, last);

		// One page for every visible bar first, so each shows its latest trades at once
		for (var i = visibleLast; i >= visibleFirst; i--)
			if (
				!IsComplete(bars[i].Time)
				&& GetCursor(bars[i].Time) == GetCoverageEnd(bars[i].Time)
			)
				return GetCursor(bars[i].Time);
		for (var i = visibleLast; i >= visibleFirst; i--)
			if (!IsComplete(bars[i].Time))
				return GetCursor(bars[i].Time);
		var goal = visibleFirst <= last ? Min(SessionStart, bars[visibleFirst].Time) : SessionStart;
		for (var i = last; i >= 0 && bars[i].Time >= goal; i--)
			if (!IsComplete(bars[i].Time))
				return GetCursor(bars[i].Time);
		return null;
	}

	/// <summary>
	/// A page of past trades (oldest first) ending where <see cref="GetNextPageEnd"/> said
	/// (<paramref name="before"/>): counted into every bar it extends back without a gap. An empty
	/// page means IB has no trades before it.
	/// </summary>
	public void AddPage(IReadOnlyList<TickByTickDto> page, DateTimeOffset before)
	{
		if (_disposed)
			return;
		Requests++;
		var inRange = page.Where(x => x.Time < before).OrderBy(x => x.Time).ToList();
		if (inRange.Count == 0)
		{
			if (before > _noTradesBefore)
				_noTradesBefore = before;
			_throttle.Request();
			return;
		}
		// IB ends a page on a whole second: every trade from the oldest one's second on is in
		var trades = inRange
			.Where(x => x.Price > 0)
			.Select(x => new FootprintTapeTradeDto(
				x.Time.ToUnixTimeSeconds(),
				x.Price,
				(double)x.Size,
				FootprintSide.Unknown
			))
			.ToList();
		Add(inRange[0].Time, before, trades);
		_throttle.Request();
	}

	/// <summary>
	/// The next UTC day whose cached trades to count before asking IB for its pages (start of the
	/// day), in the order pages would go: the visible bars' days first, newest first, then the
	/// session's; null when every day a page would go to is in.
	/// </summary>
	public DateTimeOffset? GetNextRecordedDay()
	{
		if (_disposed)
			return null;
		var bars = _getBars();
		var last = bars.Count - 1;
		while (last >= 0 && bars[last].Time >= LiveFrom)
			last--;
		if (last < 0)
			return null;
		var (visibleFirst, visibleLast) = GetVisibleRange(bars, last);
		var goal = visibleFirst <= last ? Min(SessionStart, bars[visibleFirst].Time) : SessionStart;
		foreach (
			var i in Enumerable
				.Range(0, Math.Max(0, visibleLast - visibleFirst + 1))
				.Select(x => visibleLast - x)
				.Concat(Enumerable.Range(0, last + 1).Select(x => last - x))
		)
		{
			var bar = bars[i];
			if (bar.Time < goal)
				break;
			if (IsComplete(bar.Time))
				continue;
			var end = GetCoverageEnd(bar.Time);
			foreach (
				var day in new[] { bar.Time.UtcDateTime.Date, end.AddTicks(-1).UtcDateTime.Date }
			)
				if (!_recordedDays.Contains(day))
					return new DateTimeOffset(day, TimeSpan.Zero);
		}
		return null;
	}

	/// <summary>
	/// The cached trades of the UTC day starting at <paramref name="day"/>
	/// (<see cref="GetNextRecordedDay"/>), or a slice of them, oldest first: counted where nothing
	/// is yet. Sends no request.
	/// </summary>
	public void AddRecorded(DateTimeOffset day, IReadOnlyList<FootprintTapeRangeDto> ranges)
	{
		if (_disposed)
			return;
		_recordedDays.Add(day.UtcDateTime.Date);
		foreach (var range in ranges)
			Add(range.From, range.To, range.Trades);
		_throttle.Request();
	}

	/// <summary>No more pages (a page failed, the chart left footprints).</summary>
	public void StopBackfill()
	{
		if (IsStopped)
			return;
		IsStopped = true;
		_throttle.Request();
	}

	/// <summary>A live trade, classified; from any thread. Trades before <see cref="LiveFrom"/> are the backfill's.</summary>
	public void AddLiveTrade(FootprintTradeDto trade)
	{
		if (_disposed || trade.Time < LiveFrom)
			return;
		_liveTrades.Enqueue(trade);
		if (Interlocked.Exchange(ref _liveQueued, 1) == 0)
			_post(FoldLiveTrades);
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

	/// <summary>The bar's footprint with the trades counted so far (ready or partial); null otherwise.</summary>
	public FootprintBarDto? Get(DateTimeOffset barTime) =>
		GetState(barTime) is FootprintBarState.Ready or FootprintBarState.Partial
			? _bars.GetValueOrDefault(barTime)
			: null;

	public FootprintBarState GetState(DateTimeOffset barTime)
	{
		// Live trades are missing from where the stream stopped (or never started)
		var end = barTime + _length;
		if (end > LiveFrom && _liveFailedAt is { } failedAt && end > failedAt)
			return FootprintBarState.Unavailable;
		if (barTime >= LiveFrom)
			return FootprintBarState.Ready;
		var hasTrades = _bars.TryGetValue(barTime, out var bar) && bar.Levels.Count > 0;
		if (IsComplete(barTime))
			return hasTrades || end > _noTradesBefore
				? FootprintBarState.Ready
				: FootprintBarState.Unavailable;
		if (hasTrades)
			return FootprintBarState.Partial;
		return IsStopped ? FootprintBarState.Unavailable : FootprintBarState.Loading;
	}

	/// <summary>A partial bar's trades reach back to here (they run to its end); null for others.</summary>
	public DateTimeOffset? GetLoadedFrom(DateTimeOffset barTime) =>
		GetState(barTime) == FootprintBarState.Partial ? GetCursor(barTime) : null;

	/// <summary>
	/// Every bar from here to the latest has all its trades (the oldest such bar's start); null
	/// while the newest bar before <see cref="LiveFrom"/> is still loading.
	/// </summary>
	public DateTimeOffset? GetCompleteFrom()
	{
		var bars = _getBars();
		DateTimeOffset? from = null;
		for (var i = bars.Count - 1; i >= 0; i--)
		{
			if (bars[i].Time >= LiveFrom)
				continue;
			if (!IsComplete(bars[i].Time))
				break;
			from = bars[i].Time;
		}
		return from;
	}

	/// <summary>The visible bars before <see cref="LiveFrom"/> with all their trades, of all of them.</summary>
	public (int Complete, int Total) GetVisibleProgress()
	{
		var bars = _getBars();
		var last = bars.Count - 1;
		while (last >= 0 && bars[last].Time >= LiveFrom)
			last--;
		if (last < 0)
			return (0, 0);
		var (first, visibleLast) = GetVisibleRange(bars, last);
		var complete = 0;
		for (var i = first; i <= visibleLast; i++)
			if (IsComplete(bars[i].Time))
				complete++;
		return (complete, Math.Max(0, visibleLast - first + 1));
	}

	public void Dispose()
	{
		_disposed = true;
		_throttle.Dispose();
	}

	private (int First, int Last) GetVisibleRange(IReadOnlyList<BarDto> bars, int last)
	{
		if (_visibleFrom is not { } from || _visibleTo is not { } to)
			return (Math.Max(0, last - DefaultVisibleBars + 1), last);
		var first = 0;
		while (first <= last && bars[first].Time < from)
			first++;
		var visibleLast = last;
		while (visibleLast >= first && bars[visibleLast].Time > to)
			visibleLast--;
		return (first, visibleLast);
	}

	// All of the bar's past trades are counted, from a trade before its start (or IB has none
	// before where it got to)
	private bool IsComplete(DateTimeOffset barTime) =>
		FindCovering(GetCoverageEnd(barTime)) is { } span && span.Start < barTime
		|| GetCursor(barTime) <= _noTradesBefore;

	// The bar's past trades are counted from here to its end (or LiveFrom)
	private DateTimeOffset GetCursor(DateTimeOffset barTime) =>
		FindCovering(GetCoverageEnd(barTime)) is { } span
			? Max(span.Start, barTime)
			: GetCoverageEnd(barTime);

	private DateTimeOffset GetCoverageEnd(DateTimeOffset barTime) =>
		Min(barTime + _length, LiveFrom);

	// The stretch reaching up to the time from before it (start before, end at or after)
	private Span? FindCovering(DateTimeOffset time)
	{
		var low = 0;
		var high = _spans.Count;
		while (low < high)
		{
			var middle = low + (high - low) / 2;
			if (_spans[middle].Start < time)
				low = middle + 1;
			else
				high = middle;
		}
		return low > 0 && _spans[low - 1].End >= time ? _spans[low - 1] : null;
	}

	// Every trade from `from` to before `to` (oldest first): counted into the bars where nothing
	// has been yet, the stretches merged
	private void Add(
		DateTimeOffset from,
		DateTimeOffset to,
		IReadOnlyList<FootprintTapeTradeDto> trades
	)
	{
		to = Min(to, LiveFrom);
		if (from >= to)
			return;
		var gaps = FootprintTapeHelpers.GetGaps(
			_spans.Select(x => (x.Start, x.End)).ToList(),
			from,
			to
		);
		foreach (var (gapFrom, gapTo) in gaps)
			Fill(
				gapFrom,
				gapTo,
				trades,
				FootprintTapeHelpers.LowerBound(trades, gapFrom.ToUnixTimeSeconds()),
				FootprintTapeHelpers.LowerBound(trades, gapTo.ToUnixTimeSeconds())
			);
	}

	// A gap between the stretches filled with trades[first..end): their sides by the tick rule
	// from the trade before the gap (the stretch ending at its start), the run at the start of
	// the stretch after it told by its last trade, the three merged into one
	private void Fill(
		DateTimeOffset from,
		DateTimeOffset to,
		IReadOnlyList<FootprintTapeTradeDto> trades,
		int first,
		int end
	)
	{
		var index = 0;
		while (index < _spans.Count && _spans[index].End < from)
			index++;
		var previous = index < _spans.Count && _spans[index].End == from ? _spans[index] : null;
		var nextIndex = previous != null ? index + 1 : index;
		var next =
			nextIndex < _spans.Count && _spans[nextIndex].Start == to ? _spans[nextIndex] : null;

		var lastPrice = previous?.LastPrice;
		var lastSide = previous?.LastSide ?? FootprintSide.Unknown;
		Head? head = null;
		var bars = _getBars();
		var barIndex = -1;
		var barStart = DateTimeOffset.MaxValue;
		var barEnd = DateTimeOffset.MinValue;
		FootprintBarDto? bar = null;
		for (var k = first; k < end; k++)
		{
			var trade = trades[k];
			var time = trade.Time;
			if (time < barStart || time >= barEnd)
			{
				barIndex = FindBar(bars, time, barIndex);
				bar = null;
				if (
					barIndex >= 0
					&& time < bars[barIndex].Time + _length
					&& bars[barIndex].Time < LiveFrom
				)
				{
					barStart = bars[barIndex].Time;
					barEnd = barStart + _length;
					bar = GetOrAdd(barStart);
				}
				else
				{
					// In no bar (outside the chart's hours) until the next one starts
					barStart = time;
					barEnd =
						barIndex + 1 < bars.Count
							? bars[barIndex + 1].Time
							: DateTimeOffset.MaxValue;
				}
			}
			var side =
				trade.QuoteSide != FootprintSide.Unknown
					? trade.QuoteSide
					: FootprintHelpers.ClassifyByTickRule(trade.Price, lastPrice, lastSide);
			var size = (decimal)trade.Size;
			if (bar != null)
				FootprintHelpers.AddTrade(bar, trade.Price, size, side);
			// The run at the start with no trade before it to tell its side
			if (side == FootprintSide.Unknown)
			{
				head ??= new Head { Price = trade.Price };
				if (bar != null)
					head.Trades.Add(
						(bar, FootprintHelpers.GetTickIndex(trade.Price, TickSize), size)
					);
			}
			lastPrice = trade.Price;
			lastSide = side;
		}

		// The run at the merged stretch's start still waiting for the trade before it
		var mergedHead = previous?.Head;
		if (head != null)
		{
			if (previous?.LastPrice == null)
				mergedHead = head;
			// The stretch before ends in the same run (one price, no side yet)
			else if (mergedHead != null && mergedHead.Price == head.Price)
				mergedHead.Trades.AddRange(head.Trades);
		}
		if (next?.Head is { } nextHead)
		{
			if (lastPrice is { } price)
			{
				var side = FootprintHelpers.ClassifyByTickRule(nextHead.Price, price, lastSide);
				if (side != FootprintSide.Unknown)
				{
					foreach (var (headBar, tickIndex, size) in nextHead.Trades)
						FootprintHelpers.SetSide(headBar, tickIndex, size, side);
					if (next.LastSide == FootprintSide.Unknown)
						next.LastSide = side;
				}
				else if (mergedHead != null && mergedHead.Price == nextHead.Price)
					mergedHead.Trades.AddRange(nextHead.Trades);
			}
			else
				mergedHead ??= nextHead;
		}

		var merged = new Span
		{
			Start = previous?.Start ?? from,
			End = next?.End ?? to,
			LastPrice = next?.LastPrice ?? lastPrice,
			LastSide = next?.LastPrice != null ? next.LastSide : lastSide,
			Head = mergedHead,
		};
		var removeFrom = previous != null ? index : nextIndex;
		var removeCount = (previous != null ? 1 : 0) + (next != null ? 1 : 0);
		_spans.RemoveRange(removeFrom, removeCount);
		_spans.Insert(removeFrom, merged);
	}

	// The last bar starting at or before the time (-1: none), searched from a bar known to be
	// before it
	private static int FindBar(IReadOnlyList<BarDto> bars, DateTimeOffset time, int from)
	{
		var low = Math.Max(0, from);
		var high = bars.Count;
		while (low < high)
		{
			var middle = low + (high - low) / 2;
			if (bars[middle].Time <= time)
				low = middle + 1;
			else
				high = middle;
		}
		return low - 1;
	}

	private void FoldLiveTrades()
	{
		Interlocked.Exchange(ref _liveQueued, 0);
		if (_disposed)
			return;
		var bars = _getBars();
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

	private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

	private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

	/// <summary>A stretch of time before LiveFrom whose trades are all counted.</summary>
	private sealed class Span
	{
		public DateTimeOffset Start { get; init; }
		public DateTimeOffset End { get; init; }

		/// <summary>Its last trade's price and side (null: it has none).</summary>
		public double? LastPrice { get; init; }

		public FootprintSide LastSide { get; set; }

		/// <summary>The run at its start whose side waits for the trade before it.</summary>
		public Head? Head { get; init; }
	}

	/// <summary>
	/// The run of trades at a stretch's start (one price, until the first price change) counted
	/// half on each side until the trade before it is known.
	/// </summary>
	private sealed class Head
	{
		public double Price { get; init; }
		public List<(FootprintBarDto Bar, long TickIndex, decimal Size)> Trades { get; } = [];
	}
}
