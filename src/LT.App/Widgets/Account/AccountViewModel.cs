using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Account;

public partial class AccountViewModel : ConnectedViewModelBase
{
	private readonly IAccountManager _accountManager;

	public AccountViewModel(
		IAccountManager accountManager,
		IConnectionManager connectionManager,
		AccountStore accountStore
	)
		: base(connectionManager)
	{
		_accountManager = accountManager;
		Store = accountStore;
		accountStore.PropertyChanged += OnStoreChanged;
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

	protected override void OnConnected(CancellationToken session)
	{
		if (Store.SelectedAccount is not { } account)
			return;

		_ = RunPeriodicAsync(
			ct => LoadSummaryAsync(account, ct),
			AccountConstants.SummaryRefreshInterval,
			session
		);
		_ = StreamPnlAsync(account, session);
	}

	private void OnStoreChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(AccountStore.SelectedAccount))
			return;
		Apply(new AccountSummaryDto());
		ApplyPnl(new AccountPnlDto());
		RestartSession();
	}

	private async Task LoadSummaryAsync(string account, CancellationToken cancellationToken) =>
		Apply(await _accountManager.GetSummaryAsync(account, cancellationToken));

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
		UpdatedText = summary.Account.Length > 0 ? $"Updated {summary.UpdatedAt:HH:mm:ss}" : null;
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
