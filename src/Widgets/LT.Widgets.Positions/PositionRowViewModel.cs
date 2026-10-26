using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.App.ViewModels;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Positions;

namespace LT.App.Widgets.Positions;

public partial class PositionRowViewModel(PositionDto position) : ViewModelBase
{
	public string Account { get; } = position.Account;
	public int ContractId { get; } = position.ContractId;
	public ContractDto Contract { get; } = position.Contract;
	public string Symbol { get; } = position.Symbol;
	public string SecurityType { get; } = position.SecurityType;
	public string Exchange { get; } = position.Exchange;
	public string Currency { get; } = position.Currency;

	/// <summary>The latest update, for actions on the row (closing it).</summary>
	public PositionDto Position { get; private set; } = position;

	public CancellationTokenSource? PnlSubscription { get; set; }

	public double? UnrealizedValue { get; private set; }
	public double? DailyValue { get; private set; }

	[ObservableProperty]
	public partial string Quantity { get; set; } = FormatHelpers.Quantity(position.Quantity);

	[ObservableProperty]
	public partial string AverageCost { get; set; } = FormatHelpers.Price(position.AverageCost);

	[ObservableProperty]
	public partial string MarketValue { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string UnrealizedPnl { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string DailyPnl { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string RealizedPnl { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial bool UnrealizedUp { get; set; }

	[ObservableProperty]
	public partial bool UnrealizedDown { get; set; }

	[ObservableProperty]
	public partial bool DailyUp { get; set; }

	[ObservableProperty]
	public partial bool DailyDown { get; set; }

	public void Update(PositionDto update)
	{
		Position = update;
		Quantity = FormatHelpers.Quantity(update.Quantity);
		AverageCost = FormatHelpers.Price(update.AverageCost);
	}

	public void ApplyPnl(PositionPnlDto pnl)
	{
		UnrealizedValue = pnl.UnrealizedPnl;
		DailyValue = pnl.DailyPnl;
		MarketValue = FormatHelpers.Money(pnl.MarketValue);
		UnrealizedPnl = FormatHelpers.SignedMoney(pnl.UnrealizedPnl);
		DailyPnl = FormatHelpers.SignedMoney(pnl.DailyPnl);
		RealizedPnl = FormatHelpers.SignedMoney(pnl.RealizedPnl);
		UnrealizedUp = pnl.UnrealizedPnl > 0;
		UnrealizedDown = pnl.UnrealizedPnl < 0;
		DailyUp = pnl.DailyPnl > 0;
		DailyDown = pnl.DailyPnl < 0;
	}
}
