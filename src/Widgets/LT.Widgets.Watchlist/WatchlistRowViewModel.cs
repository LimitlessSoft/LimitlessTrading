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

	// Market cap inputs: the latest flushed quote and the SEC share count (null until known)
	private QuoteDto? _quote;
	private long? _sharesOutstanding;

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
	public partial string MarketCap { get; set; } = FormatHelpers.Empty;

	/// <summary>The column's sort key; null while unknown.</summary>
	[ObservableProperty]
	public partial double? MarketCapValue { get; set; }

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
		_quote = quote;
		UpdateMarketCap();
	}

	/// <summary>The SEC share count arrived (UI thread); market cap follows every price from now on.</summary>
	public void SetSharesOutstanding(long? shares)
	{
		_sharesOutstanding = shares;
		UpdateMarketCap();
	}

	private void UpdateMarketCap()
	{
		MarketCapValue = MarketCapHelpers.Compute(_sharesOutstanding, _quote);
		MarketCap = FormatHelpers.MarketCap(MarketCapValue);
	}
}
