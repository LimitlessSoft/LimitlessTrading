using System.Globalization;
using LT.Contracts.Dtos.Options;

namespace LT.App.Widgets.GammaExposure;

/// <summary>One expiration in the picker: "Oct 30 · 0d".</summary>
public class ExpirationOption(OptionExpirationDto expiration, DateTime today)
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
		) + $" · {(_date - today.Date).Days}d";

	public override string ToString() => Label;
}
