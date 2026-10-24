using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.App.ViewModels;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Watchlist;

public partial class WatchlistRowViewModel(ContractDto contract) : ViewModelBase
{
	public ContractDto Contract { get; } = contract;
	public string Symbol { get; } = ContractHelpers.GetDisplayName(contract);
	public string Description { get; } =
		$"{contract.SecurityType} · {ContractHelpers.GetListingExchange(contract)} · {contract.Currency}";

	/// <summary>Newest quote written by the market data task; applied on the UI flush.</summary>
	public LatestValue<QuoteDto> Pending { get; } = new();

	public CancellationTokenSource? Subscription { get; set; }

	[ObservableProperty]
	public partial string Last { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string Change { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string ChangePercent { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string Bid { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string Ask { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string Volume { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial bool IsUp { get; set; }

	[ObservableProperty]
	public partial bool IsDown { get; set; }

	[ObservableProperty]
	public partial bool IsDelayed { get; set; }

	[ObservableProperty]
	public partial string? Error { get; set; }

	public void Flush()
	{
		if (Pending.Take() is not { } quote)
			return;

		var change = TickTypeHelpers.GetChange(quote);
		Last = FormatHelpers.Price(TickTypeHelpers.GetDisplayPrice(quote));
		Change = change is { } c ? c.ToString("+#,0.00;-#,0.00;0.00") : FormatHelpers.Empty;
		ChangePercent = FormatHelpers.SignedPercent(TickTypeHelpers.GetChangePercent(quote));
		Bid = FormatHelpers.Price(quote.Bid);
		Ask = FormatHelpers.Price(quote.Ask);
		Volume = FormatHelpers.Volume(quote.Volume);
		IsUp = change > 0;
		IsDown = change < 0;
		IsDelayed = quote.IsDelayed;
		Error = null;
	}
}
