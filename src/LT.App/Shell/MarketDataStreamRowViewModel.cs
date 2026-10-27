using CommunityToolkit.Mvvm.ComponentModel;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Helpers;

namespace LT.App.Shell;

/// <summary>One market data line in the status bar's stream list.</summary>
public partial class MarketDataStreamRowViewModel(MarketDataStreamDto stream, string sinceText)
	: ObservableObject
{
	public MarketDataStreamDto Stream { get; } = stream;

	public string ContractText { get; } = MarketDataStreamHelpers.GetContractText(stream);

	public string KindText { get; } = MarketDataStreamHelpers.GetKindText(stream);

	public string OwnerText { get; } = string.IsNullOrEmpty(stream.Owner) ? "—" : stream.Owner;

	/// <summary>When it was opened, in the display time zone.</summary>
	public string SinceText { get; } = sinceText;

	/// <summary>How long it has been open; ticks with the status bar's timer.</summary>
	[ObservableProperty]
	public partial string AgeText { get; set; } = string.Empty;
}
