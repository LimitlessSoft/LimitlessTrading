using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.App.ViewModels;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
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

	// Sort keys of the quote columns (Bid / Ask sorts by bid); null until the first quote
	public double? LastValue { get; private set; }
	public double? ChangePercentValue { get; private set; }
	public double? BidValue { get; private set; }
	public decimal? VolumeValue { get; private set; }

	/// <summary>Its place among the pinned rows (0 = pinned first); null when not pinned.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsPinned))]
	public partial int? PinIndex { get; set; }

	public bool IsPinned => PinIndex is not null;

	/// <summary>Its values in the watchlist's custom columns, in their order (grid binds by index).</summary>
	[ObservableProperty]
	public partial IReadOnlyList<WatchlistCellViewModel> Cells { get; set; } = [];

	/// <summary>Its saved value in a custom column; empty when it has none.</summary>
	public string GetCustomValue(string? columnId) =>
		Cells.FirstOrDefault(x => x.ColumnId == columnId)?.Text ?? string.Empty;

	[ObservableProperty]
	public partial bool IsUp { get; set; }

	[ObservableProperty]
	public partial bool IsDown { get; set; }

	[ObservableProperty]
	public partial bool IsDelayed { get; set; }

	[ObservableProperty]
	public partial string? Error { get; set; }

	/// <summary>Applies the newest quote, if one came since the last flush; true when it did.</summary>
	public bool Flush()
	{
		if (Pending.Take() is not { } quote)
			return false;

		var change = TickTypeHelpers.GetChange(quote);
		LastValue = TickTypeHelpers.GetDisplayPrice(quote);
		ChangePercentValue = TickTypeHelpers.GetChangePercent(quote);
		BidValue = quote.Bid;
		VolumeValue = quote.Volume;
		Last = FormatHelpers.Price(LastValue);
		Change = change is { } c ? c.ToString("+#,0.00;-#,0.00;0.00") : FormatHelpers.Empty;
		ChangePercent = FormatHelpers.SignedPercent(ChangePercentValue);
		Bid = FormatHelpers.Price(quote.Bid);
		Ask = FormatHelpers.Price(quote.Ask);
		Volume = FormatHelpers.Volume(quote.Volume);
		IsUp = change > 0;
		IsDown = change < 0;
		IsDelayed = quote.IsDelayed;
		Error = null;
		_quote = quote;
		UpdateMarketCap();
		return true;
	}

	/// <summary>The row's value in a sorted column; null when it has none yet.</summary>
	public IComparable? GetSortKey(WatchlistSortColumn column) =>
		column switch
		{
			WatchlistSortColumn.Symbol => Symbol,
			WatchlistSortColumn.Last => LastValue,
			WatchlistSortColumn.ChangePercent => ChangePercentValue,
			WatchlistSortColumn.Bid => BidValue,
			WatchlistSortColumn.Volume => VolumeValue,
			WatchlistSortColumn.MarketCap => MarketCapValue,
			_ => null,
		};

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
