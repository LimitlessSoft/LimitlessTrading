using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.ViewModels;

namespace LT.App.Widgets.Depth;

/// <summary>
/// One row of a ladder side. Rows are created once per side and refilled on every flush, so
/// the list never churns; an empty row shows nothing.
/// </summary>
public partial class DepthLevelViewModel(bool isBid) : ViewModelBase
{
	public bool IsBid { get; } = isBid;

	[ObservableProperty]
	public partial string? PriceText { get; set; }

	[ObservableProperty]
	public partial string? SizeText { get; set; }

	/// <summary>Size summed from the best row out to this one.</summary>
	[ObservableProperty]
	public partial string? TotalText { get; set; }

	/// <summary>Exchange or market maker (SMART / Level 2 depth); empty for one exchange.</summary>
	[ObservableProperty]
	public partial string? ExchangeText { get; set; }

	/// <summary>The row's size against the largest row on either side, 0–1.</summary>
	[ObservableProperty]
	public partial double SizeFraction { get; set; }

	/// <summary>The best bid or ask.</summary>
	[ObservableProperty]
	public partial bool IsBest { get; set; }

	public void Set(
		string price,
		string size,
		string total,
		string? exchange,
		double sizeFraction,
		bool isBest
	)
	{
		PriceText = price;
		SizeText = size;
		TotalText = total;
		ExchangeText = exchange;
		SizeFraction = sizeFraction;
		IsBest = isBest;
	}

	public void Clear()
	{
		PriceText = null;
		SizeText = null;
		TotalText = null;
		ExchangeText = null;
		SizeFraction = 0;
		IsBest = false;
	}
}
