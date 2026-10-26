using System.Globalization;
using LT.Contracts.Dtos.Options;

namespace LT.App.Widgets.OptionChain;

/// <summary>One expiration tab of the option chain: "Oct 27" over "3d".</summary>
public class ExpirationTab(OptionExpirationDto expiration, DateTime today)
{
	private readonly DateTime _date = DateTime.ParseExact(
		expiration.Expiration,
		"yyyyMMdd",
		CultureInfo.InvariantCulture
	);

	public OptionExpirationDto Expiration => expiration;

	public string Label =>
		_date.ToString(
			_date.Year == today.Year ? "MMM d" : @"MMM d \'yy",
			CultureInfo.InvariantCulture
		);

	public string DaysText => $"{(_date - today.Date).Days}d";
}
