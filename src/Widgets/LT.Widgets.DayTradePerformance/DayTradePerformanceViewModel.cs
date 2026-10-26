using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.Contracts.Dtos.Performance;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.DayTradePerformance;

/// <summary>
/// A trading day of the selected account from the local trade journal: the intraday P&amp;L curve
/// (live for today) and the day's closed trades with their statistics. Recording runs in the
/// background (TradeJournalRecorder) whether this widget is open or not; recorded days stay
/// viewable offline. Fills imported from IB Flex (history and, with a Trade Confirmation query,
/// today's) count like recorded ones, so the stats work while IB's socket executions don't.
/// </summary>
public partial class DayTradePerformanceViewModel : ConnectedViewModelBase
{
	private readonly ITradeJournalManager _journal;
	private readonly IFlexImportManager _flexImport;
	private int _curveQueued;
	private int _tradesQueued;

	public DayTradePerformanceViewModel(
		ITradeJournalManager journal,
		IFlexImportManager flexImport,
		IConnectionManager connectionManager,
		AccountStore accountStore
	)
		: base(connectionManager)
	{
		_journal = journal;
		_flexImport = flexImport;
		flexImport.StatusChanged += OnFlexStatusChanged;
		Store = accountStore;
		SelectedDay = journal.Today;
		accountStore.PropertyChanged += OnStoreChanged;
		journal.FillsChanged += OnFillsChanged;
		journal.PnlChanged += OnPnlChanged;
	}

	public AccountStore Store { get; }

	public ObservableCollection<TradeRowViewModel> Trades { get; } = [];

	[ObservableProperty]
	public partial DateOnly SelectedDay { get; set; }

	[ObservableProperty]
	public partial string DayText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool CanGoPrevious { get; set; }

	[ObservableProperty]
	public partial bool CanGoNext { get; set; }

	[ObservableProperty]
	public partial bool IsToday { get; set; }

	[ObservableProperty]
	public partial IReadOnlyList<PnlSampleDto>? Curve { get; set; }

	[ObservableProperty]
	public partial string CurvePnl { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial bool CurveUp { get; set; }

	[ObservableProperty]
	public partial bool CurveDown { get; set; }

	[ObservableProperty]
	public partial string CurveHigh { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string CurveLow { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string MaxDrawdown { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string NetPnl { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial bool NetUp { get; set; }

	[ObservableProperty]
	public partial bool NetDown { get; set; }

	[ObservableProperty]
	public partial string TradeCount { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string WinRate { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string AverageWin { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string AverageLoss { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string ProfitFactor { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string LargestWin { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string LargestLoss { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string Expectancy { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string GrossPnl { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string Commissions { get; set; } = FormatHelpers.Empty;

	/// <summary>Fills / open trades of the day, or why there are no trades.</summary>
	[ObservableProperty]
	public partial string? TradesText { get; set; }

	/// <summary>
	/// Where the trades come from: IB Flex and how delayed today's are, or why IB's executions
	/// can't be read (CVC_007 until IBKR.Net decodes them) when Flex isn't set up.
	/// </summary>
	[ObservableProperty]
	public partial string? TradesNote { get; set; }

	[RelayCommand(CanExecute = nameof(CanGoPrevious))]
	public void PreviousDay()
	{
		if (TradeJournalHelpers.GetPreviousDay(_journal.GetDays(), SelectedDay) is { } day)
			SelectedDay = day;
	}

	[RelayCommand(CanExecute = nameof(CanGoNext))]
	public void NextDay()
	{
		if (TradeJournalHelpers.GetNextDay(_journal.GetDays(), SelectedDay) is { } day)
			SelectedDay = day;
	}

	[RelayCommand]
	public void Today() => SelectedDay = _journal.Today;

	/// <summary>
	/// Asks IB for the day's executions now (F5 / the Refresh button) and reloads; also starts a
	/// Flex import (today's trades), whose fills reload the view when they arrive.
	/// </summary>
	[RelayCommand]
	public async Task RefreshAsync()
	{
		_ = Task.Run(() => _flexImport.ImportNowAsync());
		try
		{
			await _journal.RecordExecutionsAsync();
			ErrorMessage = null;
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
		Reload();
	}

	public override void Dispose()
	{
		Store.PropertyChanged -= OnStoreChanged;
		_journal.FillsChanged -= OnFillsChanged;
		_journal.PnlChanged -= OnPnlChanged;
		_flexImport.StatusChanged -= OnFlexStatusChanged;
		base.Dispose();
	}

	protected override void OnConnected(CancellationToken session) => Reload();

	protected override void OnDisconnected() => Reload();

	partial void OnSelectedDayChanged(DateOnly value) => Reload();

	partial void OnCanGoPreviousChanged(bool value) => PreviousDayCommand.NotifyCanExecuteChanged();

	partial void OnCanGoNextChanged(bool value) => NextDayCommand.NotifyCanExecuteChanged();

	private void OnStoreChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AccountStore.SelectedAccount))
			Reload();
	}

	// Both come from the recorder's threads; coalesced to one UI update per burst
	private void OnPnlChanged()
	{
		if (Interlocked.Exchange(ref _curveQueued, 1) == 0)
			Dispatcher.UIThread.Post(() =>
			{
				Interlocked.Exchange(ref _curveQueued, 0);
				if (SelectedDay == _journal.Today)
					LoadCurve();
			});
	}

	private void OnFillsChanged()
	{
		if (Interlocked.Exchange(ref _tradesQueued, 1) == 0)
			Dispatcher.UIThread.Post(() =>
			{
				Interlocked.Exchange(ref _tradesQueued, 0);
				LoadDays();
				LoadTrades();
			});
	}

	private void OnFlexStatusChanged() => Dispatcher.UIThread.Post(LoadNote);

	private void Reload()
	{
		LoadDays();
		LoadCurve();
		LoadTrades();
	}

	private void LoadDays()
	{
		var days = _journal.GetDays();
		var today = _journal.Today;
		IsToday = SelectedDay == today;
		CanGoPrevious = TradeJournalHelpers.GetPreviousDay(days, SelectedDay) != null;
		CanGoNext = TradeJournalHelpers.GetNextDay(days, SelectedDay) != null;
		var text = SelectedDay.ToString("ddd d MMM yyyy", CultureInfo.CurrentCulture);
		DayText = IsToday ? $"{text} · today" : text;
	}

	private void LoadCurve()
	{
		var samples = Store.SelectedAccount is { } account
			? _journal.GetPnlCurve(SelectedDay, account)
			: [];
		Curve = samples;
		var summary = PnlCurveHelpers.Summarize(samples);
		CurvePnl = FormatHelpers.SignedMoney(summary.Last);
		CurveUp = summary.Last > 0;
		CurveDown = summary.Last < 0;
		CurveHigh = FormatHelpers.SignedMoney(summary.High);
		CurveLow = FormatHelpers.SignedMoney(summary.Low);
		MaxDrawdown = FormatHelpers.Money(summary.MaxDrawdown);
	}

	private void LoadTrades()
	{
		var day = _journal.GetDayPerformance(SelectedDay, Store.SelectedAccount);
		var stats = day.Stats;
		var hasTrades = stats.TradeCount > 0;

		Trades.Clear();
		foreach (var trade in day.Trades.Reverse())
			Trades.Add(new TradeRowViewModel(trade));

		NetPnl = hasTrades ? FormatHelpers.SignedMoney(stats.NetPnl) : FormatHelpers.Empty;
		NetUp = hasTrades && stats.NetPnl > 0;
		NetDown = hasTrades && stats.NetPnl < 0;
		TradeCount = hasTrades
			? $"{stats.TradeCount} ({stats.Wins} W / {stats.Losses} L)"
			: FormatHelpers.Empty;
		WinRate = stats.WinRate is { } rate
			? rate.ToString("0.0%", CultureInfo.CurrentCulture)
			: FormatHelpers.Empty;
		AverageWin = FormatHelpers.SignedMoney(stats.AverageWin);
		AverageLoss = FormatHelpers.SignedMoney(stats.AverageLoss);
		ProfitFactor =
			stats.ProfitFactor is { } factor ? factor.ToString("0.00", CultureInfo.CurrentCulture)
			: hasTrades && stats.Wins > 0 ? "no losses"
			: FormatHelpers.Empty;
		LargestWin = FormatHelpers.SignedMoney(stats.LargestWin);
		LargestLoss = FormatHelpers.SignedMoney(stats.LargestLoss);
		Expectancy = FormatHelpers.SignedMoney(stats.Expectancy);
		GrossPnl = hasTrades ? FormatHelpers.SignedMoney(stats.GrossPnl) : FormatHelpers.Empty;
		Commissions = hasTrades ? FormatHelpers.Money(stats.Commissions) : FormatHelpers.Empty;

		var open = day.OpenTrades > 0 ? $" · {day.OpenTrades} still open (not counted)" : "";
		TradesText =
			hasTrades ? $"{day.FillCount} fills{open}"
			: day.FillCount > 0 ? $"No closed trades · {day.FillCount} fills{open}"
			: "No fills recorded for this day.";
		LoadNote();
	}

	private void LoadNote() =>
		TradesNote =
			FlexStatusHelpers.DescribeTodayTrades(_flexImport.GetStatus())
			?? (
				_journal.ExecutionsError is { } error
					? $"{error} Set up IB Flex in Settings → Account history to import trades."
					: null
			);
}
