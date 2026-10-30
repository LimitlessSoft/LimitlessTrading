using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Calendar;

namespace LT.App.Widgets.Calendar;

/// <summary>
/// Market-wide earnings, other corporate events and economic releases of today, this week, next
/// week or this month, grouped by day, from the sources switched on in Settings → Calendar
/// (Nasdaq without any setup or IB connection; IB's Wall Street Horizon with its subscription).
/// Clicking an event sends its stock to the link group (needs IB). Never an unexplained empty
/// list: no source on, a failed source, an unreadable answer and "no events" each say so. Events
/// already past are dimmed and a "now" line sits between them and the ones to come, both kept
/// current by a <see cref="TimeProvider"/> timer due when the next event passes (or at New York
/// midnight, which loads the range again); opening the widget or switching the range scrolls to
/// it. Earnings rows of today and the few days before without a reported EPS are looked up in
/// the earnings result sources (<see cref="IEarningsResultManager"/>) after every load and filled
/// in place; while today still has such rows, they are looked up again on the sources' interval
/// (a <see cref="TimeProvider"/> timer, New York 06:00–22:00), so after-close results come in
/// without pressing Refresh. An earnings row whose reported EPS comes in or changes while the
/// widget is open (a poll's result, or a reload's) is highlighted for
/// <see cref="EarningsResultConstants.ActualHighlightDuration"/>, ended by a
/// <see cref="TimeProvider"/> timer; figures already there when a row is first seen, including
/// those its first lookup finds, are not.
/// </summary>
public partial class CalendarViewModel : ConnectedViewModelBase, ILinkable, IWidgetState
{
	private readonly ICalendarManager _calendarManager;
	private readonly IEarningsResultManager _earningsResultManager;
	private readonly IContractManager _contractManager;
	private readonly DisplayTimeStore _displayTime;
	private readonly SettingsNavigator _settingsNavigator;
	private readonly TimeProvider _timeProvider;
	private readonly TimeZoneInfo _zone = TradeJournalHelpers.GetTradeDayTimeZone();
	private readonly CancellationTokenSource _lifetime = new();
	private ITimer? _clock;
	private DateOnly _shownToday;
	private CalendarNowMarkerViewModel? _nowMarker;
	private bool _scrollToNow = true;
	private CancellationToken _session;
	private CancellationTokenSource? _load;
	private CancellationTokenSource? _resolve;
	private CancellationTokenSource? _lookup;
	private ITimer? _poll;
	private CalendarResultDto? _shown;

	// Results found by the sources, kept across loads (the rows are rebuilt by each)
	private readonly Dictionary<(string Symbol, DateOnly Date), EarningsResultDto> _results = [];

	// The reported EPS last seen per earnings row (symbol, day and its source; kept across loads),
	// and when it last came in or changed while the widget was open
	private readonly Dictionary<
		(string Symbol, DateOnly Date, CalendarSource Source),
		decimal?
	> _actuals = [];
	private readonly Dictionary<
		(string Symbol, DateOnly Date, CalendarSource Source),
		DateTimeOffset
	> _actualUpdatedAt = [];

	// Rows first seen whose first lookup hasn't finished: what it finds was already out, no highlight
	private readonly HashSet<(string Symbol, DateOnly Date, CalendarSource Source)> _unsettled = [];
	private ITimer? _highlight;

	public CalendarViewModel(
		ICalendarManager calendarManager,
		IEarningsResultManager earningsResultManager,
		IContractManager contractManager,
		IConnectionManager connectionManager,
		LinkGroupStore linkGroupStore,
		DisplayTimeStore displayTime,
		SettingsNavigator settingsNavigator,
		TimeProvider timeProvider
	)
		: base(connectionManager)
	{
		_timeProvider = timeProvider;
		_calendarManager = calendarManager;
		_earningsResultManager = earningsResultManager;
		_contractManager = contractManager;
		_displayTime = displayTime;
		_settingsNavigator = settingsNavigator;
		Link = new ContractLink(linkGroupStore, () => null);
		EmptyText = "Loading…";
		calendarManager.SourcesChanged += OnSourcesChanged;
		// After the base's connection check (posted first): connected, OnConnected has loaded
		Dispatcher.UIThread.Post(() =>
		{
			if (!IsConnected)
				Load(refresh: false);
		});
	}

	/// <summary>Clicking an event sends its stock to the link group; nothing comes back.</summary>
	public ContractLink Link { get; }

	/// <summary>
	/// Raised on the UI thread with the line the view scrolls to the top: the "now" line, else
	/// today's or the next day's header.
	/// </summary>
	public event Action<CalendarItemViewModel>? ScrollRequested;

	/// <summary>Day headers, each followed by its events; the "now" line when the range holds today.</summary>
	public ObservableCollection<CalendarItemViewModel> Items { get; } = [];

	[ObservableProperty]
	[NotifyPropertyChangedFor(
		nameof(IsToday),
		nameof(IsThisWeek),
		nameof(IsNextWeek),
		nameof(IsThisMonth)
	)]
	public partial CalendarRange Range { get; set; } = CalendarRange.ThisWeek;

	public bool IsToday
	{
		get => Range == CalendarRange.Today;
		set => SetRange(value, CalendarRange.Today);
	}

	[ObservableProperty]
	[NotifyPropertyChangedFor(
		nameof(IsAllEvents),
		nameof(IsEarnings),
		nameof(IsEconomic),
		nameof(IsOtherEvents)
	)]
	public partial CalendarEventFilter Filter { get; set; } = CalendarEventFilter.All;

	public bool IsThisWeek
	{
		get => Range == CalendarRange.ThisWeek;
		set => SetRange(value, CalendarRange.ThisWeek);
	}

	public bool IsNextWeek
	{
		get => Range == CalendarRange.NextWeek;
		set => SetRange(value, CalendarRange.NextWeek);
	}

	public bool IsThisMonth
	{
		get => Range == CalendarRange.ThisMonth;
		set => SetRange(value, CalendarRange.ThisMonth);
	}

	public bool IsAllEvents
	{
		get => Filter == CalendarEventFilter.All;
		set => SetFilter(value, CalendarEventFilter.All);
	}

	public bool IsEarnings
	{
		get => Filter == CalendarEventFilter.Earnings;
		set => SetFilter(value, CalendarEventFilter.Earnings);
	}

	public bool IsEconomic
	{
		get => Filter == CalendarEventFilter.Economic;
		set => SetFilter(value, CalendarEventFilter.Economic);
	}

	public bool IsOtherEvents
	{
		get => Filter == CalendarEventFilter.Other;
		set => SetFilter(value, CalendarEventFilter.Other);
	}

	[ObservableProperty]
	public partial CalendarItemViewModel? SelectedItem { get; set; }

	[ObservableProperty]
	public partial bool IsLoading { get; set; }

	/// <summary>"142 events · 26 Oct – 1 Nov · updated 19:03 · Nasdaq".</summary>
	[ObservableProperty]
	public partial string? StatusText { get; set; }

	/// <summary>Sources that failed while others answered: "IB Wall Street Horizon: …", one per line.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasSourceWarning))]
	public partial string? SourceWarningText { get; set; }

	public bool HasSourceWarning => SourceWarningText != null;

	/// <summary>Why there is nothing to show and what to do: no source on, no WSH subscription, unreadable data.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasNotice))]
	public partial string? NoticeText { get; set; }

	[ObservableProperty]
	public partial string? NoticeTitle { get; set; }

	public bool HasNotice => NoticeText != null;

	/// <summary>Loading, or a range without events.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasEmptyText))]
	public partial string? EmptyText { get; set; }

	public bool HasEmptyText => EmptyText != null;

	/// <summary>F5 and the Refresh button: asks the sources again instead of answering from memory.</summary>
	[RelayCommand]
	public void Refresh() => Load(refresh: true);

	/// <summary>Opens Settings → Calendar, where the sources are switched on and off.</summary>
	[RelayCommand]
	private void OpenSources() => _settingsNavigator.Open(SettingsPageNames.Calendar);

	public void LoadState(WidgetPlacementDto placement)
	{
		if (placement.CalendarRange is { } range && Enum.IsDefined(range))
			Range = range;
		if (placement.CalendarFilter is { } filter && Enum.IsDefined(filter))
			Filter = filter;
	}

	public void SaveState(WidgetPlacementDto placement)
	{
		placement.CalendarRange = Range;
		placement.CalendarFilter = Filter;
	}

	public override void Dispose()
	{
		_calendarManager.SourcesChanged -= OnSourcesChanged;
		_lifetime.Cancel();
		CancelLoad();
		StopClock();
		StopPoll();
		StopHighlight();
		_resolve?.Cancel();
		_resolve?.Dispose();
		Link.Dispose();
		base.Dispose();
		_lifetime.Dispose();
	}

	// Loads again either way: WSH needs the connection, Nasdaq answers from memory
	protected override void OnConnected(CancellationToken session)
	{
		_session = session;
		Load(refresh: false);
	}

	protected override void OnDisconnected() => Load(refresh: false);

	private void OnSourcesChanged() => Dispatcher.UIThread.Post(() => Load(refresh: false));

	partial void OnRangeChanged(CalendarRange value)
	{
		_scrollToNow = true;
		Load(refresh: false);
	}

	partial void OnFilterChanged(CalendarEventFilter value) => Load(refresh: false);

	partial void OnSelectedItemChanged(CalendarItemViewModel? value)
	{
		if (value is CalendarEventRowViewModel row)
			_ = SendToLinkGroupAsync(row.Event);
	}

	// The newest load wins: switching range or filter mid-load cancels the one running
	private async void Load(bool refresh)
	{
		if (_lifetime.IsCancellationRequested)
			return;
		CancelLoad();
		var load = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
		_load = load;
		IsLoading = true;
		ErrorMessage = null;
		if (Items.Count == 0 && NoticeText == null)
			EmptyText = "Loading…";
		try
		{
			var result = await _calendarManager.GetEventsAsync(
				new GetCalendarEventsRequest
				{
					Range = Range,
					Filter = Filter,
					Refresh = refresh,
				},
				load.Token
			);
			if (!load.IsCancellationRequested)
			{
				Show(result);
				LookUpResults(refresh);
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			if (!load.IsCancellationRequested)
			{
				StopClock();
				StopPoll();
				ClearItems();
				NoticeText = null;
				StatusText = null;
				SourceWarningText = null;
				EmptyText = null;
				ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
			}
		}
		finally
		{
			if (_load == load)
				IsLoading = false;
		}
	}

	private void Show(CalendarResultDto result)
	{
		StopClock();
		StopPoll();
		ClearItems();
		_shown = result.Status == CalendarStatus.Loaded ? result : null;
		if (result.Status != CalendarStatus.Loaded)
		{
			NoticeTitle = result.Status switch
			{
				CalendarStatus.NotSubscribed => "Wall Street Horizon subscription needed",
				CalendarStatus.Unreadable => "Calendar data could not be read",
				CalendarStatus.NoSources => "No calendar source is on",
				_ => "The calendar could not be loaded",
			};
			NoticeText = result.Message;
			StatusText = null;
			SourceWarningText = null;
			EmptyText = null;
			return;
		}

		NoticeText = null;
		SourceWarningText =
			result.SourceErrors.Count == 0
				? null
				: string.Join(
					Environment.NewLine,
					result.SourceErrors.Select(x =>
						$"{CalendarHelpers.GetSourceName(x.Source)}: {x.Message}"
					)
				);
		var now = _timeProvider.GetUtcNow();
		var today = CalendarHelpers.GetToday(now);
		_shownToday = today;
		foreach (var day in result.Days)
		{
			// The figure columns' captions: in the header when the day starts with earnings,
			// else over its first earnings row (below the releases, labelled their own way)
			var firstEarnings = day.Events.FirstOrDefault(x =>
				x.Kind == CalendarEventKind.Earnings
			);
			var captionsInHeader = firstEarnings != null && day.Events[0] == firstEarnings;
			Items.Add(
				new CalendarDayHeaderViewModel(
					day.Date,
					FormatDay(day.Date, today),
					day.Events.Count == 1 ? "1 event" : $"{day.Events.Count} events",
					day.IsTruncated
						? $"WSH returns at most {CalendarConstants.MaxEventsPerRequest} events per day; there may be more."
						: null,
					day.Date == today,
					day.Date < today,
					captionsInHeader
				)
			);
			foreach (var item in day.Events)
			{
				var passedAt = CalendarHelpers.GetPassedAt(item, _zone);
				var row = new CalendarEventRowViewModel(
					item,
					CalendarHelpers.FormatTime(item.Time, item.TimeText),
					passedAt,
					now >= passedAt,
					!captionsInHeader && item == firstEarnings,
					x => _displayTime.Format(x, "HH:mm")
				)
				{
					Result =
						item.Kind == CalendarEventKind.Earnings
							? _results.GetValueOrDefault(
								(EarningsResultHelpers.GetSymbolKey(item.Symbol), item.Date)
							)
							: null,
				};
				Items.Add(row);
				TrackActual(row, now);
			}
		}
		StartHighlight(now);
		if (Items.Count > 0 && result.Start <= today && today <= result.End)
		{
			_nowMarker = new CalendarNowMarkerViewModel();
			PlaceNowMarker();
		}
		StartClock(now);
		if (_scrollToNow)
		{
			_scrollToNow = false;
			RequestScrollToNow();
		}
		var count = result.Days.Sum(x => x.Events.Count);
		var range = $"{FormatShort(result.Start)} – {FormatShort(result.End)}";
		var sources = string.Join(" + ", result.Sources.Select(CalendarHelpers.GetSourceName));
		StatusText =
			$"{count} {(count == 1 ? "event" : "events")} · {range} · updated {_displayTime.Format(result.FetchedAt, "HH:mm")} · {sources}";
		EmptyText =
			count > 0 ? null
			: Filter == CalendarEventFilter.Earnings ? $"No earnings dates between {range}."
			: Filter == CalendarEventFilter.Economic ? $"No economic releases between {range}."
			: Filter == CalendarEventFilter.Other ? $"No corporate events between {range}."
			: $"No events between {range}.";
	}

	// The event's stock, looked up by symbol (its IB listing when WSH named the contract id)
	private async Task SendToLinkGroupAsync(CalendarEventDto item)
	{
		if (item.Symbol.Length == 0 || !IsConnected)
			return;
		_resolve?.Cancel();
		_resolve?.Dispose();
		var resolve = CancellationTokenSource.CreateLinkedTokenSource(_session);
		_resolve = resolve;
		try
		{
			var listings = await _contractManager.SearchSymbolAsync(item.Symbol, resolve.Token);
			var contract = (
				listings.FirstOrDefault(x => item.ConId > 0 && x.Contract.ConId == item.ConId)
				?? listings.FirstOrDefault()
			)?.Contract;
			if (contract != null && !resolve.IsCancellationRequested)
				Link.Publish(contract);
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			if (!resolve.IsCancellationRequested)
				ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	// Events turn past, the "now" line moves down; a new day loads the range again (its dates,
	// today's header and which days are past all change)
	private void OnClock()
	{
		if (_lifetime.IsCancellationRequested)
			return;
		var now = _timeProvider.GetUtcNow();
		if (CalendarHelpers.GetToday(now) != _shownToday)
		{
			StopClock();
			Load(refresh: false);
			return;
		}
		foreach (var row in Items.OfType<CalendarEventRowViewModel>())
			row.IsPast = now >= row.PassedAt;
		PlaceNowMarker();
		StartClock(now);
	}

	// The shown earnings rows of the lookup days still without figures, asked for day by day and
	// filled in place (no reload, the list keeps its scroll); then the next look while today has
	// some left. The newest lookup wins: a load or a poll cancels the one running
	private async void LookUpResults(bool refresh)
	{
		if (_lifetime.IsCancellationRequested || _shown is not { } shown)
			return;
		StopPoll();
		_lookup?.Cancel();
		_lookup?.Dispose();
		var lookup = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
		_lookup = lookup;
		var today = CalendarHelpers.GetToday(_timeProvider.GetUtcNow());
		var days = EarningsResultHelpers.GetLookupDays(shown.Start, shown.End, today);
		try
		{
			foreach (var day in days)
			{
				var symbols = GetPendingRows(day)
					.Select(x => x.Event.Symbol)
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();
				if (symbols.Count == 0)
					continue;
				var results = await _earningsResultManager.GetResultsAsync(
					new GetEarningsResultsRequest
					{
						Date = day,
						Symbols = symbols,
						Refresh = refresh,
					},
					lookup.Token
				);
				if (lookup.IsCancellationRequested || _shown != shown)
					return;
				Apply(results);
			}
		}
		catch (OperationCanceledException) when (lookup.IsCancellationRequested)
		{
			return;
		}
		catch (Exception)
		{
			// The sources log their own failures; the calendar shows what it has
		}
		// The rows shown so far have had their first look: what comes from now on is news
		if (_lookup == lookup && _shown == shown)
			_unsettled.Clear();
		if (_lookup != lookup || _shown != shown || !days.Contains(today))
			return;
		if (GetPendingRows(today).Any())
			StartPoll();
	}

	private IEnumerable<CalendarEventRowViewModel> GetPendingRows(DateOnly day) =>
		Items
			.OfType<CalendarEventRowViewModel>()
			.Where(x =>
				x.Event.Date == day && EarningsResultHelpers.NeedsResult(x.Event, x.Result)
			);

	private void Apply(IReadOnlyList<EarningsResultDto> results)
	{
		var now = _timeProvider.GetUtcNow();
		foreach (var result in results)
		{
			var key = (EarningsResultHelpers.GetSymbolKey(result.Symbol), result.Date);
			var combined = EarningsResultHelpers.Combine(_results.GetValueOrDefault(key), result);
			_results[key] = combined;
			foreach (
				var row in Items
					.OfType<CalendarEventRowViewModel>()
					.Where(x =>
						x.Event.Kind == CalendarEventKind.Earnings
						&& x.Event.Date == result.Date
						&& EarningsResultHelpers.GetSymbolKey(x.Event.Symbol) == key.Item1
					)
			)
			{
				row.Result = combined;
				TrackActual(row, now);
			}
		}
		StartHighlight(now);
	}

	// A row seen for the first time keeps its figure unhighlighted (and whatever its first lookup
	// finds); after that a figure coming in or changing marks it fresh
	private void TrackActual(CalendarEventRowViewModel row, DateTimeOffset now)
	{
		if (row.Event.Kind != CalendarEventKind.Earnings || row.Event.Symbol.Length == 0)
			return;
		var key = (
			EarningsResultHelpers.GetSymbolKey(row.Event.Symbol),
			row.Event.Date,
			row.Event.Source
		);
		var actual = row.EpsActual;
		if (!_actuals.TryGetValue(key, out var previous))
		{
			_actuals[key] = actual;
			_unsettled.Add(key);
		}
		else if (_unsettled.Contains(key))
			_actuals[key] = actual;
		else if (EarningsResultHelpers.IsActualUpdate(previous, actual))
		{
			_actuals[key] = actual;
			_actualUpdatedAt[key] = now;
		}
		row.IsActualFresh = EarningsResultHelpers.IsActualFresh(
			_actualUpdatedAt.TryGetValue(key, out var at) ? at : null,
			now
		);
	}

	// Highlights whose time is up are switched off, the timer set for the next one to end
	private void OnHighlight()
	{
		if (_lifetime.IsCancellationRequested)
			return;
		var now = _timeProvider.GetUtcNow();
		foreach (var key in _actualUpdatedAt.Keys.ToList())
			if (!EarningsResultHelpers.IsActualFresh(_actualUpdatedAt[key], now))
				_actualUpdatedAt.Remove(key);
		foreach (var row in Items.OfType<CalendarEventRowViewModel>().Where(x => x.IsActualFresh))
			row.IsActualFresh = _actualUpdatedAt.ContainsKey(
				(
					EarningsResultHelpers.GetSymbolKey(row.Event.Symbol),
					row.Event.Date,
					row.Event.Source
				)
			);
		StartHighlight(now);
	}

	private void StartHighlight(DateTimeOffset now)
	{
		StopHighlight();
		if (EarningsResultHelpers.GetNextHighlightEnd(_actualUpdatedAt.Values, now) is not { } end)
			return;
		_highlight = _timeProvider.CreateTimer(
			_ => Dispatcher.UIThread.Post(OnHighlight),
			null,
			end - now,
			Timeout.InfiniteTimeSpan
		);
	}

	private void StopHighlight()
	{
		_highlight?.Dispose();
		_highlight = null;
	}

	private void StartPoll()
	{
		var now = _timeProvider.GetUtcNow();
		if (
			EarningsResultHelpers.GetPollDelay(
				now,
				_earningsResultManager.GetPollInterval(now),
				_zone
			)
			is not { } delay
		)
			return;
		_poll = _timeProvider.CreateTimer(
			_ => Dispatcher.UIThread.Post(() => LookUpResults(refresh: false)),
			null,
			delay,
			Timeout.InfiniteTimeSpan
		);
	}

	private void StopPoll()
	{
		_poll?.Dispose();
		_poll = null;
	}

	private void StartClock(DateTimeOffset now)
	{
		StopClock();
		var next = CalendarHelpers.GetNextChange(
			Items.OfType<CalendarEventRowViewModel>().Select(x => x.PassedAt),
			now,
			_zone
		);
		var due = next - now;
		_clock = _timeProvider.CreateTimer(
			_ => Dispatcher.UIThread.Post(OnClock),
			null,
			due < TimeSpan.Zero ? TimeSpan.Zero : due,
			Timeout.InfiniteTimeSpan
		);
	}

	private void StopClock()
	{
		_clock?.Dispose();
		_clock = null;
	}

	private void ClearItems()
	{
		Items.Clear();
		_nowMarker = null;
	}

	// Before the first event still to come; when that event opens a later day, before its header,
	// and when it opens today, after today's header. Only between events: not above the first
	// line (nothing has passed yet) nor below the last (everything has)
	private void PlaceNowMarker()
	{
		if (_nowMarker == null)
			return;
		var current = Items.IndexOf(_nowMarker);
		if (current >= 0)
			Items.RemoveAt(current);
		var index = -1;
		for (var i = 0; i < Items.Count; i++)
		{
			if (Items[i] is not CalendarEventRowViewModel { IsPast: false })
				continue;
			index =
				i > 0
				&& Items[i - 1] is CalendarDayHeaderViewModel header
				&& header.Date > _shownToday
					? i - 1
					: i;
			break;
		}
		if (index > 0)
			Items.Insert(index, _nowMarker);
	}

	private void RequestScrollToNow()
	{
		var target =
			(_nowMarker != null && Items.Contains(_nowMarker) ? _nowMarker : null)
			?? (CalendarItemViewModel?)
				Items
					.OfType<CalendarDayHeaderViewModel>()
					.FirstOrDefault(x => x.Date >= _shownToday);
		if (target != null)
			ScrollRequested?.Invoke(target);
	}

	private void CancelLoad()
	{
		_load?.Cancel();
		_load?.Dispose();
		_load = null;
	}

	private void SetRange(bool on, CalendarRange range)
	{
		if (on)
			Range = range;
		else
		{
			// A range button can't be switched off by clicking it again; it stays checked, and
			// scrolls back to now
			RequestScrollToNow();
			OnPropertyChanged(nameof(IsToday));
			OnPropertyChanged(nameof(IsThisWeek));
			OnPropertyChanged(nameof(IsNextWeek));
			OnPropertyChanged(nameof(IsThisMonth));
		}
	}

	private void SetFilter(bool on, CalendarEventFilter filter)
	{
		if (on)
			Filter = filter;
		else
		{
			OnPropertyChanged(nameof(IsAllEvents));
			OnPropertyChanged(nameof(IsEarnings));
			OnPropertyChanged(nameof(IsEconomic));
			OnPropertyChanged(nameof(IsOtherEvents));
		}
	}

	private static string FormatDay(DateOnly date, DateOnly today)
	{
		var text = date.ToString("ddd d MMM", CultureInfo.InvariantCulture);
		return date == today ? $"{text} · Today"
			: date == today.AddDays(1) ? $"{text} · Tomorrow"
			: text;
	}

	private static string FormatShort(DateOnly date) =>
		date.ToString("d MMM", CultureInfo.InvariantCulture);
}
