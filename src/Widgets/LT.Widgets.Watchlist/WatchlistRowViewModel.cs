using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.App.ViewModels;
using LT.Contracts.Constants;
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

	/// <summary>Its live quote stream; null while it isn't streaming (off screen, or no free line).</summary>
	public CancellationTokenSource? Subscription { get; set; }

	/// <summary>When it went off screen while streaming; its line is released a little later.</summary>
	public DateTimeOffset? HiddenSince { get; set; }

	/// <summary>When its last quote (live or snapshot) was shown; null before the first.</summary>
	public DateTimeOffset? LastQuoteAt { get; set; }

	/// <summary>A snapshot of it is running.</summary>
	public bool IsSnapshotRunning { get; set; }

	/// <summary>After a failed snapshot, the next one waits until then.</summary>
	public DateTimeOffset? SnapshotRetryAt { get; set; }

	/// <summary>After finding no free line, streaming it live is tried again from then.</summary>
	public DateTimeOffset? LiveRetryAt { get; set; }

	/// <summary>The last error written to the message log for it, so a repeat isn't logged again.</summary>
	public string? LoggedError { get; set; }

	/// <summary>
	/// On screen but not streaming: no market data line was free, so its quote is a snapshot,
	/// refreshed every <c>MarketDataConstants.WatchlistSnapshotRefreshInterval</c>.
	/// </summary>
	[ObservableProperty]
	public partial bool IsLineLimited { get; set; }

	/// <summary>The tooltip of the row's "S" mark (<see cref="IsLineLimited"/>).</summary>
	public static string LineLimitedTip { get; } =
		"No free market data line: a snapshot, refreshed about every "
		+ $"{MarketDataConstants.WatchlistSnapshotRefreshInterval.TotalSeconds:0} seconds";

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
	public partial string AverageVolume { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string VolumeRate { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string AverageVolumeRate { get; set; } = FormatHelpers.Empty;

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

	/// <summary>
	/// The 90-day average daily volume; kept once known, as snapshots (no generic ticks) and
	/// lines opened without the column don't carry it.
	/// </summary>
	public decimal? AverageVolumeValue { get; private set; }

	/// <summary>
	/// IB's volume rate in shares per minute; kept once known like the average volume, as only
	/// lines with its generic tick carry it.
	/// </summary>
	public decimal? VolumeRateValue { get; private set; }

	/// <summary>The average volume per regular-session minute (Avg Vol/Min); null until known.</summary>
	public decimal? AverageVolumeRateValue { get; private set; }

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
		if (quote.AverageVolume is { } averageVolume)
		{
			AverageVolumeValue = averageVolume;
			AverageVolume = FormatHelpers.Volume(averageVolume);
			AverageVolumeRateValue = TickTypeHelpers.GetAverageVolumePerMinute(quote);
			AverageVolumeRate = FormatHelpers.Volume(AverageVolumeRateValue);
		}
		if (quote.VolumeRate is { } volumeRate)
		{
			VolumeRateValue = (decimal)volumeRate;
			VolumeRate = FormatHelpers.Volume(VolumeRateValue);
		}
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
			WatchlistSortColumn.AverageVolume => AverageVolumeValue,
			WatchlistSortColumn.VolumeRate => VolumeRateValue,
			WatchlistSortColumn.AverageVolumeRate => AverageVolumeRateValue,
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
