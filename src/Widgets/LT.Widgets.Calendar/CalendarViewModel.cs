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
/// it.
/// </summary>
public partial class CalendarViewModel : ConnectedViewModelBase, ILinkable, IWidgetState
{
	private readonly ICalendarManager _calendarManager;
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

	public CalendarViewModel(
		ICalendarManager calendarManager,
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
				Show(result);
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			if (!load.IsCancellationRequested)
			{
				StopClock();
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
		ClearItems();
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
			Items.Add(
				new CalendarDayHeaderViewModel(
					day.Date,
					FormatDay(day.Date, today),
					day.Events.Count == 1 ? "1 event" : $"{day.Events.Count} events",
					day.IsTruncated
						? $"WSH returns at most {CalendarConstants.MaxEventsPerRequest} events per day; there may be more."
						: null,
					day.Date == today,
					day.Date < today
				)
			);
			foreach (var item in day.Events)
			{
				var passedAt = CalendarHelpers.GetPassedAt(item, _zone);
				Items.Add(
					new CalendarEventRowViewModel(
						item,
						CalendarHelpers.FormatTime(item.Time, item.TimeText),
						passedAt,
						now >= passedAt
					)
				);
			}
		}
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
