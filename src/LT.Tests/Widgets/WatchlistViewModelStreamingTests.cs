using Avalonia.Threading;
using LT.App.Stores;
using LT.App.Widgets.Watchlist;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Widgets;

/// <summary>
/// A connected watchlist streams only the rows on screen (and a buffer around them); the rest
/// get snapshots a few at a time and keep their values. Without a free line, rows on screen fall
/// back to snapshots instead of showing errors.
/// </summary>
public class WatchlistViewModelStreamingTests : IDisposable
{
	private const int RowCount = 60;

	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new()
	{
		IsConnected = true,
		KeepMarketDataOpen = true,
		Ticks = [new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 10 }],
		SnapshotTicks = _ => [new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 50 }],
	};
	private readonly FakeConnectionManager _connection = new();
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 28, 15, 0, 0, TimeSpan.Zero)
	);
	private readonly MessageLogManager _log = new();
	private readonly MarketDataManager _marketData;
	private readonly WatchlistManager _watchlists;
	private WatchlistViewModel? _widget;

	public WatchlistViewModelStreamingTests()
	{
		_marketData = new MarketDataManager(_ibkr, _time);
		_watchlists = new WatchlistManager(
			_cache.Settings,
			new ContractManager(_ibkr, _cache.Settings, _cache.Manager)
		);
		var id = _watchlists.GetActive().Id;
		for (var i = 1; i <= RowCount; i++)
			_watchlists.Add(id, Stock(i));
		_connection.SetStatus(ConnectionState.Connected, "DU1");
	}

	[Fact]
	public void Before_the_grid_reports_the_first_rows_stream_and_some_others_get_snapshots()
	{
		var widget = Create();

		WaitFor(() =>
			_ibkr.MarketDataRequested.Count == MarketDataConstants.WatchlistInitialLiveRows
			&& _ibkr.SnapshotsRequested.Count == MarketDataConstants.WatchlistSnapshotsInFlight
		);

		Assert.Equal(
			Enumerable.Range(1, MarketDataConstants.WatchlistInitialLiveRows),
			StreamedConIds()
		);
		Assert.All(
			_ibkr.SnapshotsRequested,
			x => Assert.True(x.ConId > MarketDataConstants.WatchlistInitialLiveRows)
		);
		Assert.Equal(MarketDataConstants.WatchlistInitialLiveRows, _marketData.ActiveLines);
		Assert.All(widget.Rows, x => Assert.False(x.IsLineLimited));
	}

	[Fact]
	public void Rows_scrolled_away_give_their_line_back_after_a_short_delay()
	{
		var widget = Create();
		WaitFor(() =>
			_ibkr.MarketDataRequested.Count == MarketDataConstants.WatchlistInitialLiveRows
		);

		// Rows 11-20 on screen: 6-25 stream (buffer of 5)
		Ui(() => widget.SetShownRows([.. widget.Rows.Skip(10).Take(10)]));
		var buffer = MarketDataConstants.WatchlistLiveRowBuffer;
		var live = Enumerable.Range(11 - buffer, 10 + 2 * buffer).ToList();

		// Not before the delay: scrolling back would resubscribe
		Ui(widget.RefreshOffScreen);
		Assert.Equal(MarketDataConstants.WatchlistInitialLiveRows, _marketData.ActiveLines);

		_time.Advance(MarketDataConstants.WatchlistLiveReleaseDelay);
		Ui(widget.RefreshOffScreen);

		// Released as each stream's reader ends
		WaitFor(() =>
			_marketData.ActiveLines == live.Count && _ibkr.OpenMarketDataStreams == live.Count
		);
		Assert.Equal(
			live,
			widget.Rows.Where(x => x.Subscription != null).Select(x => x.Contract.ConId)
		);
		// Nothing was asked for twice
		Assert.Equal(MarketDataConstants.WatchlistInitialLiveRows, _ibkr.MarketDataRequested.Count);
	}

	[Fact]
	public void A_hidden_widget_streams_nothing_and_its_rows_keep_their_values()
	{
		var widget = Create();
		WaitFor(() =>
			_ibkr.MarketDataRequested.Count == MarketDataConstants.WatchlistInitialLiveRows
		);
		WaitFor(() =>
		{
			widget.FlushQuotes();
			return widget.Rows[0].LastValue == 10;
		});

		Ui(() => widget.SetShownRows([]));
		_time.Advance(MarketDataConstants.WatchlistLiveReleaseDelay);
		Ui(widget.RefreshOffScreen);

		WaitFor(() => _marketData.ActiveLines == 0);
		Assert.Equal(10, widget.Rows[0].LastValue);
	}

	[Fact]
	public void Off_screen_rows_get_snapshots_a_batch_at_a_time_and_keep_the_values_for_sorting()
	{
		_ibkr.SnapshotGate = new TaskCompletionSource(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		var widget = Create();
		Ui(() => widget.SetShownRows([.. widget.Rows.Take(5)]));
		WaitFor(() =>
			_ibkr.SnapshotsRequested.Count == MarketDataConstants.WatchlistSnapshotsInFlight
		);

		// A batch is running: the next tick asks for no more
		Ui(widget.RefreshOffScreen);
		Assert.Equal(
			MarketDataConstants.WatchlistSnapshotsInFlight,
			_ibkr.SnapshotsRequested.Count
		);

		_ibkr.SnapshotGate.SetResult();
		var first = _ibkr.SnapshotsRequested.Select(x => x.ConId).ToHashSet();
		WaitFor(() =>
		{
			widget.FlushQuotes();
			return widget.Rows.Count(x => x.LastValue == 50) == first.Count;
		});
		Assert.All(
			widget.Rows.Where(x => first.Contains(x.Contract.ConId)),
			x => Assert.Equal(50, x.LastValue)
		);

		// The next batch: rows never quoted come before the ones just refreshed
		Ui(widget.RefreshOffScreen);
		WaitFor(() =>
			_ibkr.SnapshotsRequested.Count == 2 * MarketDataConstants.WatchlistSnapshotsInFlight
		);
		Assert.DoesNotContain(
			_ibkr.SnapshotsRequested.Skip(MarketDataConstants.WatchlistSnapshotsInFlight),
			x => first.Contains(x.ConId)
		);
	}

	[Fact]
	public void Without_a_free_line_rows_on_screen_show_snapshots_without_errors()
	{
		// Every line the watchlist may take is in use elsewhere
		for (
			var i = 0;
			i < MarketDataConstants.MaxLines - MarketDataConstants.WatchlistReservedLines;
			i++
		)
			_marketData.AcquireLine(MarketDataStreamKind.News, new ContractDto());
		var widget = Create();
		Ui(() => widget.SetShownRows([.. widget.Rows.Take(3)]));

		WaitFor(() => _ibkr.SnapshotsRequested.Count > 0);

		Assert.Empty(_ibkr.MarketDataRequested);
		var onScreen = widget.Rows.Take(3 + MarketDataConstants.WatchlistLiveRowBuffer).ToList();
		Assert.All(onScreen, x => Assert.True(x.IsLineLimited));
		Assert.All(widget.Rows, x => Assert.Null(x.Error));
		Assert.Single(_log.GetAll(), x => x.Text.Contains("no free market data line"));
		Assert.Contains(_ibkr.SnapshotsRequested, x => x.ConId == 1);
	}

	[Fact]
	public void Ibs_no_free_line_error_turns_rows_into_snapshot_rows_logged_once()
	{
		_ibkr.MarketDataExceptionFor = _ => new IbkrRequestException(
			101,
			"Max number of tickers has been reached"
		);
		var widget = Create();
		Ui(() => widget.SetShownRows([.. widget.Rows.Take(3)]));

		WaitFor(() =>
			widget
				.Rows.Take(3 + MarketDataConstants.WatchlistLiveRowBuffer)
				.All(x => x.IsLineLimited)
		);

		Assert.All(widget.Rows, x => Assert.Null(x.Error));
		Assert.Single(_log.GetAll(), x => x.Text.Contains("no free market data line"));
		Assert.Equal(0, _marketData.ActiveLines);

		// They are tried again later, not on every tick
		var asked = _ibkr.MarketDataRequested.Count;
		Ui(widget.RefreshOffScreen);
		Assert.Equal(asked, _ibkr.MarketDataRequested.Count);
		_ibkr.MarketDataExceptionFor = null;
		_time.Advance(MarketDataConstants.WatchlistSnapshotRefreshInterval);
		Ui(widget.RefreshOffScreen);
		WaitFor(() => widget.Rows.Take(3).All(x => !x.IsLineLimited));
	}

	[Fact]
	public void A_snapshot_error_shows_on_the_row_logged_once_and_waits_before_asking_again()
	{
		_ibkr.SnapshotExceptionFor = x =>
			x.ConId == RowCount ? new IbkrRequestException(354, "Not subscribed.") : null;
		var widget = Create();
		Ui(() => widget.SetShownRows([.. widget.Rows.Take(5)]));
		var last = widget.Rows[^1];

		for (var i = 0; i < 10 && last.Error == null; i++)
		{
			_time.Advance(MarketDataConstants.WatchlistSnapshotRefreshInterval);
			Ui(widget.RefreshOffScreen);
			WaitFor(() => widget.Rows.All(x => !x.IsSnapshotRunning));
		}
		Assert.NotNull(last.Error);
		var asked = _ibkr.SnapshotsRequested.Count(x => x.ConId == RowCount);

		_time.Advance(MarketDataConstants.WatchlistSnapshotRefreshInterval);
		Ui(widget.RefreshOffScreen);
		WaitFor(() => widget.Rows.All(x => !x.IsSnapshotRunning));

		Assert.Equal(asked, _ibkr.SnapshotsRequested.Count(x => x.ConId == RowCount));
		Assert.Single(_log.GetAll(), x => x.Text.Contains("Not subscribed."));
	}

	public void Dispose()
	{
		if (_widget != null)
			TestUiThread.Invoke(_widget.Dispose);
		GC.SuppressFinalize(this);
	}

	[Fact]
	public void Rows_stream_average_volume_and_reopen_without_it_when_its_column_is_hidden()
	{
		var widget = Create();
		var live = MarketDataConstants.WatchlistInitialLiveRows;
		WaitFor(() => _ibkr.MarketDataRequested.Count == live);
		lock (_ibkr.MarketDataRequested)
			Assert.All(
				_ibkr.GenericTicksRequested,
				x => Assert.Equal(MarketDataConstants.AverageVolumeGenericTick, x)
			);

		Ui(() => widget.SetColumnShown(WatchlistSortColumn.AverageVolume, null, false));

		// Each live row asks again, without the generic tick, and the old lines are released
		WaitFor(() =>
			_ibkr.MarketDataRequested.Count == 2 * live
			&& _marketData.ActiveLines == live
			&& _ibkr.OpenMarketDataStreams == live
		);
		lock (_ibkr.MarketDataRequested)
			Assert.All(_ibkr.GenericTicksRequested.Skip(live), x => Assert.Equal("", x));

		// Sorted by it while hidden: asked for again
		Ui(() =>
			widget.SortBy(
				WatchlistSortColumn.AverageVolume,
				null,
				System.ComponentModel.ListSortDirection.Descending
			)
		);
		WaitFor(() => _ibkr.MarketDataRequested.Count == 3 * live);
		lock (_ibkr.MarketDataRequested)
			Assert.All(
				_ibkr.GenericTicksRequested.Skip(2 * live),
				x => Assert.Equal(MarketDataConstants.AverageVolumeGenericTick, x)
			);
	}

	private static ContractDto Stock(int conId) =>
		new()
		{
			ConId = conId,
			Symbol = $"S{conId}",
			SecurityType = "STK",
			Exchange = "SMART",
			Currency = "USD",
		};

	private IEnumerable<int> StreamedConIds()
	{
		lock (_ibkr.MarketDataRequested)
			return [.. _ibkr.MarketDataRequested.Select(x => x.ConId).Order()];
	}

	private static void Ui(Action action) =>
		TestUiThread.Invoke(() =>
		{
			action();
			Dispatcher.UIThread.RunJobs();
		});

	// Runs posted UI work until the condition holds (checked on the UI thread)
	private static void WaitFor(Func<bool> condition)
	{
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (
			!TestUiThread.Invoke(() =>
			{
				Dispatcher.UIThread.RunJobs();
				return condition();
			})
		)
		{
			if (DateTime.UtcNow > deadline)
				throw new TimeoutException("condition not met");
			Thread.Sleep(10);
		}
	}

	// Connected before it is made: its first posted job connects it
	private WatchlistViewModel Create()
	{
		_widget = TestUiThread.Invoke(() =>
		{
			var widget = new WatchlistViewModel(
				_watchlists,
				new ContractManager(_ibkr, _cache.Settings, _cache.Manager),
				_marketData,
				_connection,
				_log,
				new SettingsManager(_cache.Settings, _ibkr),
				new SharesOutstandingManager(
					new FakeSecApiClient(),
					new InMemorySharesOutstandingRepository(),
					_log,
					_cache.Time
				),
				new LinkGroupStore(),
				_time
			);
			Dispatcher.UIThread.RunJobs();
			return widget;
		});
		return _widget;
	}
}
