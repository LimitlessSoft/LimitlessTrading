using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.App.ViewModels;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.OptionChain;

public partial class OptionRowViewModel(OptionStrikeDto strike, bool isAtm) : ViewModelBase
{
	public OptionStrikeDto Strike { get; } = strike;
	public string StrikeText { get; } = strike.Strike.ToString("#,0.##");
	public bool IsAtm { get; } = isAtm;

	public LatestValue<QuoteDto> CallPending { get; } = new();
	public LatestValue<QuoteDto> PutPending { get; } = new();

	[ObservableProperty]
	public partial string CallBid { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string CallAsk { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string CallLast { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string CallVolume { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string PutBid { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string PutAsk { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string PutLast { get; set; } = FormatHelpers.Empty;

	[ObservableProperty]
	public partial string PutVolume { get; set; } = FormatHelpers.Empty;

	public void Flush()
	{
		if (CallPending.Take() is { } call)
		{
			CallBid = FormatHelpers.Price(call.Bid);
			CallAsk = FormatHelpers.Price(call.Ask);
			CallLast = FormatHelpers.Price(TickTypeHelpers.GetDisplayPrice(call));
			CallVolume = FormatHelpers.Volume(call.Volume);
		}
		if (PutPending.Take() is { } put)
		{
			PutBid = FormatHelpers.Price(put.Bid);
			PutAsk = FormatHelpers.Price(put.Ask);
			PutLast = FormatHelpers.Price(TickTypeHelpers.GetDisplayPrice(put));
			PutVolume = FormatHelpers.Volume(put.Volume);
		}
	}
}
