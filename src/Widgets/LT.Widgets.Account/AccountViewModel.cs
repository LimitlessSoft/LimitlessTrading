using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Account;

public partial class AccountViewModel : ConnectedViewModelBase
{
	private readonly IAccountManager _accountManager;
	private readonly DisplayTimeStore _displayTime;
	private readonly ZonedText _updatedText;
	private readonly ZonedText _staleText;

	public AccountViewModel(
		IAccountManager accountManager,
		IConnectionManager connectionManager,
		AccountStore accountStore,
		DisplayTimeStore displayTime
	)
		: base(connectionManager)
	{
		_accountManager = accountManager;
		_displayTime = displayTime;
		_updatedText = new ZonedText(x => UpdatedText = x);
		_staleText = new ZonedText(x => StaleText = x);
		displayTime.ZoneChanged += OnZoneChanged;
		Store = accountStore;
		accountStore.PropertyChanged += OnStoreChanged;
		// After the base class has applied the connection state
		Dispatcher.UIThread.Post(() =>
		{
			if (!IsConnected)
				ShowSnapshot(null);
		});
	}

	public AccountStore Store { get; }

	[ObservableProperty]
	public partial string NetLiquidation { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string TotalCash { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string BuyingPower { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string AvailableFunds { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string ExcessLiquidity { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string InitMargin { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string MaintMargin { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string GrossPositionValue { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string Currency { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string? UpdatedText { get; set; }

	/// <summary>Set while the values are the account's last-known summary, not live.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsStale))]
	[NotifyPropertyChangedFor(nameof(ShowConnectHint))]
	public partial string? StaleText { get; set; }

	public bool IsStale => StaleText != null;

	public bool ShowConnectHint => !IsConnected && !IsStale;

	[ObservableProperty]
	public partial string DailyPnl { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string UnrealizedPnl { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string RealizedPnl { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial bool DailyUp { get; set; }

	[ObservableProperty]
	public partial bool DailyDown { get; set; }

	[ObservableProperty]
	public partial bool UnrealizedUp { get; set; }

	[ObservableProperty]
	public partial bool UnrealizedDown { get; set; }

	[ObservableProperty]
	public partial bool RealizedUp { get; set; }

	[ObservableProperty]
	public partial bool RealizedDown { get; set; }

	public void Refresh() => RestartSession();

	protected override void OnPropertyChanged(PropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		if (e.PropertyName == nameof(IsConnected))
			OnPropertyChanged(nameof(ShowConnectHint));
	}

	protected override void OnDisconnected()
	{
		ApplyPnl(new AccountPnlDto());
		ShowSnapshot(null);
	}

	protected override void OnConnected(CancellationToken session)
	{
		if (Store.SelectedAccount is not { } account)
			return;

		_ = StreamSummaryAsync(account, session);
		_ = StreamPnlAsync(account, session);
	}

	private void OnZoneChanged()
	{
		_updatedText.Rebuild();
		_staleText.Rebuild();
	}

	private void OnStoreChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(AccountStore.SelectedAccount))
			return;
		Apply(new AccountSummaryDto());
		ApplyPnl(new AccountPnlDto());
		_staleText.Clear();
		if (IsConnected)
			RestartSession();
		else
			ShowSnapshot(null);
	}

	private async Task StreamSummaryAsync(string account, CancellationToken cancellationToken)
	{
		var live = false;
		try
		{
			await foreach (
				var summary in _accountManager.SubscribeSummaryAsync(account, cancellationToken)
			)
			{
				live = true;
				_staleText.Clear();
				Apply(summary);
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
			if (!live)
				ShowSnapshot(ErrorMessage);
		}
	}

	private async Task StreamPnlAsync(string account, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var pnl in _accountManager.SubscribePnlAsync(account, cancellationToken))
				ApplyPnl(pnl);
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	private void Apply(AccountSummaryDto summary)
	{
		NetLiquidation = FormatHelpers.Money(summary.NetLiquidation);
		TotalCash = FormatHelpers.Money(summary.TotalCashValue);
		BuyingPower = FormatHelpers.Money(summary.BuyingPower);
		AvailableFunds = FormatHelpers.Money(summary.AvailableFunds);
		ExcessLiquidity = FormatHelpers.Money(summary.ExcessLiquidity);
		InitMargin = FormatHelpers.Money(summary.InitMarginReq);
		MaintMargin = FormatHelpers.Money(summary.MaintMarginReq);
		GrossPositionValue = FormatHelpers.Money(summary.GrossPositionValue);
		Currency = summary.Currency;
		if (summary.Account.Length > 0)
			_updatedText.Set(() => $"Updated {_displayTime.Format(summary.UpdatedAt)}");
		else
			_updatedText.Clear();
	}

	// Display only, never for decisions (buying power for an order is always asked live)
	private void ShowSnapshot(string? error)
	{
		_staleText.Clear();
		Apply(new AccountSummaryDto());
		if (Store.SnapshotAccount is not { } account)
			return;
		if (_accountManager.GetSummarySnapshot(account) is not { } snapshot)
			return;
		Apply(snapshot.Value);
		_updatedText.Clear();
		_staleText.Set(() =>
			CacheHelpers.FormatStale(
				_displayTime.ToDisplay(snapshot.FetchedAt),
				_displayTime.Now,
				account,
				error
			)
		);
	}

	private void ApplyPnl(AccountPnlDto pnl)
	{
		DailyPnl = FormatHelpers.SignedMoney(pnl.DailyPnl);
		UnrealizedPnl = FormatHelpers.SignedMoney(pnl.UnrealizedPnl);
		RealizedPnl = FormatHelpers.SignedMoney(pnl.RealizedPnl);
		DailyUp = pnl.DailyPnl > 0;
		DailyDown = pnl.DailyPnl < 0;
		UnrealizedUp = pnl.UnrealizedPnl > 0;
		UnrealizedDown = pnl.UnrealizedPnl < 0;
		RealizedUp = pnl.RealizedPnl > 0;
		RealizedDown = pnl.RealizedPnl < 0;
	}
}
