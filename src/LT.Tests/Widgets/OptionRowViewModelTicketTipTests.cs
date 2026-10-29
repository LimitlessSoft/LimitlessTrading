using System.Globalization;
using LT.App.Widgets.OptionChain;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Options;

namespace LT.Tests.Widgets;

/// <summary>
/// The strike cell's C / P halves: hovering one says a click opens a ticket for that option and
/// that it drags onto a chart, only where the strike has that option. No order is ever sent.
/// </summary>
public class OptionRowViewModelTicketTipTests : IDisposable
{
	private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
	private readonly OptionRowViewModel _row;

	public OptionRowViewModelTicketTipTests()
	{
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		_row = new OptionRowViewModel(
			new OptionStrikeDto
			{
				Strike = 150,
				Call = new ContractDto
				{
					ConId = 11,
					Symbol = "SPY",
					SecurityType = "OPT",
					Right = "C",
					Strike = 150,
				},
			},
			(_, _, _) => throw new InvalidOperationException("No order may be sent"),
			() => true
		);
	}

	[Fact]
	public void A_half_with_an_option_says_it_drags_onto_a_chart() =>
		Assert.Equal(
			"Order the 150.00 call: market or limit, reviewed before it is sent\nDrag onto a chart to show it there",
			_row.CallTicketTip
		);

	[Fact]
	public void A_half_without_an_option_offers_no_drag() =>
		Assert.Equal("No put at this strike", _row.PutTicketTip);

	public void Dispose()
	{
		CultureInfo.CurrentCulture = _culture;
		GC.SuppressFinalize(this);
	}
}
