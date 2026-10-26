using System.ComponentModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.Contracts.Dtos.AccountPerformance;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.AccountPerformance;

/// <summary>
/// Equity curve, return stats, daily P&amp;L calendar and bars of the selected account, from the
/// history IAccountPerformanceManager records while the app is connected and imports from IB Flex
/// (IFlexImportManager). Today follows the live values. History shows while disconnected too,
/// once an account has been picked.
/// </summary>
public partial class AccountPerformanceViewModel : ConnectedViewModelBase
{
	private readonly IAccountPerformanceManager _performanceManager;
	private readonly IFlexImportManager _flexImport;
	private readonly SettingsNavigator _settingsNavigator;
	private IReadOnlyList<AccountDaySnapshotDto> _history = [];
	private int _reloadPending;

	public AccountPerformanceViewModel(
		IAccountPerformanceManager performanceManager,
		IFlexImportManager flexImport,
		IConnectionManager connectionManager,
		AccountStore accountStore,
		SettingsNavigator settingsNavigator
	)
		: base(connectionManager)
	{
		_performanceManager = performanceManager;
		_flexImport = flexImport;
		_settingsNavigator = settingsNavigator;
		flexImport.StatusChanged += OnFlexStatusChanged;
		Store = accountStore;
		CalendarMonth = AccountPerformanceHelpers.GetMonthStart(performanceManager.GetToday());
		accountStore.PropertyChanged += OnStoreChanged;
		performanceManager.HistoryChanged += OnHistoryChanged;
		Reload();
	}

	public AccountStore Store { get; }

	[ObservableProperty]
	public partial PerformanceRange Range { get; set; } = PerformanceRange.Month;

	public bool IsWeekRange
	{
		get => Range == PerformanceRange.Week;
		set
		{
			if (value)
				Range = PerformanceRange.Week;
			else // unchecking the picked range keeps it picked
				OnPropertyChanged(nameof(IsWeekRange));
		}
	}

	public bool IsMonthRange
	{
		get => Range == PerformanceRange.Month;
		set
		{
			if (value)
				Range = PerformanceRange.Month;
			else // unchecking the picked range keeps it picked
				OnPropertyChanged(nameof(IsMonthRange));
		}
	}

	public bool IsYearToDateRange
	{
		get => Range == PerformanceRange.YearToDate;
		set
		{
			if (value)
				Range = PerformanceRange.YearToDate;
			else // unchecking the picked range keeps it picked
				OnPropertyChanged(nameof(IsYearToDateRange));
		}
	}

	public bool IsAllRange
	{
		get => Range == PerformanceRange.All;
		set
		{
			if (value)
				Range = PerformanceRange.All;
			else // unchecking the picked range keeps it picked
				OnPropertyChanged(nameof(IsAllRange));
		}
	}

	/// <summary>Recorded days in the picked range, for the equity curve and the P&amp;L bars.</summary>
	[ObservableProperty]
	public partial IReadOnlyList<AccountDaySnapshotDto> RangeDays { get; set; } = [];

	[ObservableProperty]
	public partial IReadOnlyList<ReturnStatViewModel> Stats { get; set; } = [];

	[ObservableProperty]
	public partial string MaxDrawdown { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial bool MaxDrawdownDown { get; set; }

	[ObservableProperty]
	public partial string Currency { get; set; } = string.Empty;

	/// <summary>First day of the month the calendar shows.</summary>
	[ObservableProperty]
	public partial DateOnly CalendarMonth { get; set; }

	[ObservableProperty]
	public partial string MonthTitle { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string MonthTotal { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial bool MonthUp { get; set; }

	[ObservableProperty]
	public partial bool MonthDown { get; set; }

	[ObservableProperty]
	public partial bool CanGoNextMonth { get; set; }

	[ObservableProperty]
	public partial IReadOnlyList<CalendarDayViewModel> CalendarDays { get; set; } = [];

	[ObservableProperty]
	public partial string HistoryNote { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool HasAccount { get; set; }

	/// <summary>The Flex setup banner's text; null hides it (FlexStatusHelpers.GetBanner).</summary>
	[ObservableProperty]
	public partial string? FlexBanner { get; set; }

	[ObservableProperty]
	public partial bool FlexBannerIsWarning { get; set; }

	/// <summary>The banner's Set up…: Settings on Account history → IB Flex.</summary>
	[RelayCommand]
	private void SetUpFlex() => _settingsNavigator.Open(SettingsPageNames.AccountHistory);

	/// <summary>F5: reloads, and starts a Flex import if one is due (its days reload the view).</summary>
	public void Refresh()
	{
		Reload();
		_ = Task.Run(() => _flexImport.ImportNowAsync());
	}

	public override void Dispose()
	{
		Store.PropertyChanged -= OnStoreChanged;
		_performanceManager.HistoryChanged -= OnHistoryChanged;
		_flexImport.StatusChanged -= OnFlexStatusChanged;
		base.Dispose();
	}

	protected override void OnConnected(CancellationToken session) => Reload();

	partial void OnRangeChanged(PerformanceRange value)
	{
		OnPropertyChanged(nameof(IsWeekRange));
		OnPropertyChanged(nameof(IsMonthRange));
		OnPropertyChanged(nameof(IsYearToDateRange));
		OnPropertyChanged(nameof(IsAllRange));
		Rebuild();
	}

	[RelayCommand]
	private void PreviousMonth()
	{
		CalendarMonth = CalendarMonth.AddMonths(-1);
		Rebuild();
	}

	[RelayCommand]
	private void NextMonth()
	{
		if (!CanGoNextMonth)
			return;
		CalendarMonth = CalendarMonth.AddMonths(1);
		Rebuild();
	}

	private void OnStoreChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(AccountStore.SelectedAccount))
			return;
		CalendarMonth = AccountPerformanceHelpers.GetMonthStart(_performanceManager.GetToday());
		Reload();
	}

	// Any thread, with every P&L tick: one reload per dispatcher pass at most
	private void OnHistoryChanged(string account)
	{
		if (account != Store.SelectedAccount || Interlocked.Exchange(ref _reloadPending, 1) == 1)
			return;
		Dispatcher.UIThread.Post(
			() =>
			{
				Volatile.Write(ref _reloadPending, 0);
				Reload();
			},
			DispatcherPriority.Background
		);
	}

	private void OnFlexStatusChanged() => Dispatcher.UIThread.Post(UpdateHistoryNote);

	private void Reload()
	{
		var account = Store.SelectedAccount;
		HasAccount = account != null;
		_history = account != null ? _performanceManager.GetHistory(account) : [];
		Rebuild();
	}

	private void Rebuild()
	{
		var today = _performanceManager.GetToday();

		RangeDays = AccountPerformanceHelpers.FilterRange(
			_history,
			AccountPerformanceHelpers.GetRangeStart(Range, today)
		);

		Stats =
		[
			new ReturnStatViewModel(
				"Today",
				AccountPerformanceHelpers.GetPeriodReturn(_history, today, today)
			),
			new ReturnStatViewModel(
				"This week",
				AccountPerformanceHelpers.GetPeriodReturn(
					_history,
					AccountPerformanceHelpers.GetWeekStart(today),
					today
				)
			),
			new ReturnStatViewModel(
				"This month",
				AccountPerformanceHelpers.GetPeriodReturn(
					_history,
					AccountPerformanceHelpers.GetMonthStart(today),
					today
				)
			),
			new ReturnStatViewModel(
				"Year to date",
				AccountPerformanceHelpers.GetPeriodReturn(
					_history,
					AccountPerformanceHelpers.GetYearStart(today),
					today
				)
			),
		];

		var drawdown = AccountPerformanceHelpers.GetMaxDrawdown(RangeDays);
		MaxDrawdown = drawdown.Money is { } money
			? $"{FormatHelpers.SignedMoney(money)}   {FormatHelpers.SignedPercent(drawdown.Percent)}"
			: FormatHelpers.Empty;
		MaxDrawdownDown = drawdown.Money < 0;

		Currency = _history.LastOrDefault(x => x.Currency.Length > 0)?.Currency ?? string.Empty;

		var monthStart = AccountPerformanceHelpers.GetMonthStart(CalendarMonth);
		MonthTitle = monthStart.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
		var monthPnl = AccountPerformanceHelpers
			.GetPeriodReturn(_history, monthStart, monthStart.AddMonths(1).AddDays(-1))
			.Pnl;
		MonthTotal = FormatHelpers.SignedMoney(monthPnl);
		MonthUp = monthPnl > 0;
		MonthDown = monthPnl < 0;
		CanGoNextMonth = monthStart < AccountPerformanceHelpers.GetMonthStart(today);
		CalendarDays = AccountPerformanceHelpers
			.BuildCalendarMonth(monthStart, _history, today)
			.Select(x => new CalendarDayViewModel(x))
			.ToList();

		UpdateHistoryNote();
	}

	private void UpdateHistoryNote()
	{
		var flex = _flexImport.GetStatus();
		var banner = FlexStatusHelpers.GetBanner(flex);
		FlexBanner = banner?.Text;
		FlexBannerIsWarning = banner?.IsWarning ?? false;

		// The banner says what needs the user; the note only says where the days come from
		var firstFlex = _history.FirstOrDefault(x => x.Source == AccountDaySource.Flex);
		var source =
			firstFlex != null
				? $"From IB Flex since {FormatDate(firstFlex.Date)}: end-of-day NAV, with deposits and withdrawals left out of P&L. Today is live."
			: _history.Count > 0
				? $"Recorded while the app was running, since {FormatDate(_history[0].Date)}."
			: flex.IsConfigured ? "Waiting for the IB Flex import."
			: "";
		var state =
			flex.Activity is { } activity ? $" {activity}…"
			: banner == null && flex.LastError is { } error
				? $" Last IB Flex import failed: {error}"
			: "";
		HistoryNote = (source + state).Trim();
	}

	private static string FormatDate(DateOnly date) =>
		date.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
}
