using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.ViewModels;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.OptionChain;

/// <summary>
/// One strike of the chain. Its bid / ask cells trade with one click through
/// <paramref name="quickTrade"/> (the chain's, which holds the managers and the double-send
/// guard); <paramref name="canTrade"/> says whether the chain may trade at all (connected).
/// </summary>
public partial class OptionRowViewModel(
	OptionStrikeDto strike,
	Func<OptionRowViewModel, string, OptionQuoteColumn, Task> quickTrade,
	Func<bool> canTrade
) : ViewModelBase
{
	private QuoteDto? _callQuote;
	private QuoteDto? _putQuote;

	public OptionStrikeDto Strike { get; } = strike;
	public string StrikeText { get; } = FormatHelpers.FixedPrice(strike.Strike);

	/// <summary>The strike nearest the underlying price.</summary>
	[ObservableProperty]
	public partial bool IsAtm { get; set; }

	/// <summary>Distance from the price in standard deviations of the expected move.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsWithinOneSigma))]
	[NotifyPropertyChangedFor(nameof(IsWithinTwoSigma))]
	[NotifyPropertyChangedFor(nameof(IsWithinThreeSigma))]
	[NotifyPropertyChangedFor(nameof(IsBeyondThreeSigma))]
	public partial SigmaBand SigmaBand { get; set; }

	/// <summary>Strike cell tooltip, e.g. "+1.4σ from the underlying"; null when unknown.</summary>
	[ObservableProperty]
	public partial string? SigmaText { get; set; }

	public bool IsWithinOneSigma => SigmaBand == SigmaBand.WithinOne;
	public bool IsWithinTwoSigma => SigmaBand == SigmaBand.WithinTwo;
	public bool IsWithinThreeSigma => SigmaBand == SigmaBand.WithinThree;
	public bool IsBeyondThreeSigma => SigmaBand == SigmaBand.BeyondThree;

	[ObservableProperty]
	public partial bool IsCallInTheMoney { get; set; }

	[ObservableProperty]
	public partial bool IsPutInTheMoney { get; set; }

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

	/// <summary>
	/// Re-shades the row for a new underlying price (null: unknown, nothing shaded) and expected
	/// move <paramref name="sigma"/> (null: no σ band).
	/// </summary>
	public void SetUnderlyingPrice(double? price, double? atmStrike, double? sigma)
	{
		IsAtm = atmStrike == Strike.Strike;
		var distance = OptionChainHelpers.GetSigmaDistance(Strike.Strike, price, sigma);
		SigmaBand = OptionChainHelpers.GetSigmaBand(distance);
		SigmaText = OptionChainHelpers.FormatSigma(distance) is { } text
			? $"{text} from the underlying"
			: null;
		IsCallInTheMoney = OptionChainHelpers.IsInTheMoney(
			Strike.Strike,
			MarketDataConstants.CallRight,
			price
		);
		IsPutInTheMoney = OptionChainHelpers.IsInTheMoney(
			Strike.Strike,
			MarketDataConstants.PutRight,
			price
		);
	}

	public void Flush()
	{
		var changed = false;
		if (CallPending.Take() is { } call)
		{
			_callQuote = call;
			changed = true;
			CallBid = FormatHelpers.FixedPrice(call.Bid);
			CallAsk = FormatHelpers.FixedPrice(call.Ask);
			CallLast = FormatHelpers.FixedPrice(TickTypeHelpers.GetDisplayPrice(call));
			CallVolume = FormatHelpers.WholeNumber(call.Volume);
		}
		if (PutPending.Take() is { } put)
		{
			_putQuote = put;
			changed = true;
			PutBid = FormatHelpers.FixedPrice(put.Bid);
			PutAsk = FormatHelpers.FixedPrice(put.Ask);
			PutLast = FormatHelpers.FixedPrice(TickTypeHelpers.GetDisplayPrice(put));
			PutVolume = FormatHelpers.WholeNumber(put.Volume);
		}
		if (changed)
			RefreshTrading();
	}

	/// <summary>Re-evaluates which cells can be clicked (a quote arrived, connection changed).</summary>
	public void RefreshTrading()
	{
		BuyCallCommand.NotifyCanExecuteChanged();
		SellCallCommand.NotifyCanExecuteChanged();
		BuyPutCommand.NotifyCanExecuteChanged();
		SellPutCommand.NotifyCanExecuteChanged();
	}

	// Ask cells buy, bid cells sell; disabled with no connection, contract or price to hit
	[RelayCommand(CanExecute = nameof(CanBuyCall))]
	private Task BuyCallAsync() =>
		quickTrade(this, MarketDataConstants.CallRight, OptionQuoteColumn.Ask);

	[RelayCommand(CanExecute = nameof(CanSellCall))]
	private Task SellCallAsync() =>
		quickTrade(this, MarketDataConstants.CallRight, OptionQuoteColumn.Bid);

	[RelayCommand(CanExecute = nameof(CanBuyPut))]
	private Task BuyPutAsync() =>
		quickTrade(this, MarketDataConstants.PutRight, OptionQuoteColumn.Ask);

	[RelayCommand(CanExecute = nameof(CanSellPut))]
	private Task SellPutAsync() =>
		quickTrade(this, MarketDataConstants.PutRight, OptionQuoteColumn.Bid);

	private bool CanBuyCall() => CanTrade(MarketDataConstants.CallRight, OptionQuoteColumn.Ask);

	private bool CanSellCall() => CanTrade(MarketDataConstants.CallRight, OptionQuoteColumn.Bid);

	private bool CanBuyPut() => CanTrade(MarketDataConstants.PutRight, OptionQuoteColumn.Ask);

	private bool CanSellPut() => CanTrade(MarketDataConstants.PutRight, OptionQuoteColumn.Bid);

	private bool CanTrade(string right, OptionQuoteColumn column) =>
		canTrade()
		&& OptionChainHelpers.CanQuickTrade(
			Strike,
			right,
			column,
			right == MarketDataConstants.CallRight ? _callQuote : _putQuote
		);
}
