namespace LT.Contracts.Helpers;

/// <summary>
/// Which flag stands for a country or currency: "United States", "USD" and "US" are all
/// <c>US</c>; the euro area ("Euro Zone", "EUR") is <c>EU</c>. Codes are ISO 3166-1 alpha-2 (plus
/// <c>EU</c>), and only the ones a flag is drawn for (<see cref="Codes"/>): anything else is
/// null, so the caller keeps showing the text.
/// </summary>
public static class CountryFlagHelpers
{
	/// <summary>Every code a flag is drawn for.</summary>
	public static IReadOnlyCollection<string> Codes => ByCode.Keys;

	// Code → the country names (as Nasdaq's economic calendar and common usage write them) and
	// currencies that stand for it
	private static readonly Dictionary<string, string[]> ByCode = new(
		StringComparer.OrdinalIgnoreCase
	)
	{
		["US"] = ["United States", "United States of America", "USA", "U.S.", "USD"],
		["EU"] =
		[
			"Euro Zone",
			"Eurozone",
			"Euro Area",
			"European Union",
			"European Monetary Union",
			"EMU",
			"EUR",
		],
		["GB"] = ["United Kingdom", "UK", "U.K.", "Great Britain", "Britain", "England", "GBP"],
		["JP"] = ["Japan", "JPY"],
		["CN"] = ["China", "People's Republic of China", "CNY", "CNH", "RMB"],
		["DE"] = ["Germany"],
		["FR"] = ["France"],
		["IT"] = ["Italy"],
		["ES"] = ["Spain"],
		["CA"] = ["Canada", "CAD"],
		["AU"] = ["Australia", "AUD"],
		["NZ"] = ["New Zealand", "NZD"],
		["CH"] = ["Switzerland", "CHF"],
		["SE"] = ["Sweden", "SEK"],
		["NO"] = ["Norway", "NOK"],
		["DK"] = ["Denmark", "DKK"],
		["FI"] = ["Finland"],
		["IS"] = ["Iceland", "ISK"],
		["IE"] = ["Ireland"],
		["NL"] = ["Netherlands", "The Netherlands", "Holland"],
		["BE"] = ["Belgium"],
		["AT"] = ["Austria"],
		["PT"] = ["Portugal"],
		["GR"] = ["Greece"],
		["PL"] = ["Poland", "PLN"],
		["CZ"] = ["Czech Republic", "Czechia", "CZK"],
		["HU"] = ["Hungary", "HUF"],
		["RU"] = ["Russia", "Russian Federation", "RUB"],
		["UA"] = ["Ukraine", "UAH"],
		["TR"] = ["Turkey", "Türkiye", "Turkiye", "TRY"],
		["IN"] = ["India", "INR"],
		["KR"] = ["South Korea", "Korea", "Republic of Korea", "KRW"],
		["HK"] = ["Hong Kong", "HKD"],
		["SG"] = ["Singapore", "SGD"],
		["TW"] = ["Taiwan", "TWD"],
		["TH"] = ["Thailand", "THB"],
		["ID"] = ["Indonesia", "IDR"],
		["ZA"] = ["South Africa", "ZAR"],
		["BR"] = ["Brazil", "BRL"],
		["MX"] = ["Mexico", "MXN"],
		["AR"] = ["Argentina", "ARS"],
		["CL"] = ["Chile", "CLP"],
		["CO"] = ["Colombia", "COP"],
		["IL"] = ["Israel", "ILS"],
		["SA"] = ["Saudi Arabia", "SAR"],
		["AE"] = ["United Arab Emirates", "UAE", "AED"],
	};

	private static readonly Dictionary<string, string> ByName = BuildNames();

	/// <summary>
	/// The flag's code for a country name, a currency or a code ("Euro Zone", "EUR", "eu" →
	/// <c>EU</c>); null when no flag is drawn for it.
	/// </summary>
	public static string? GetCode(string? countryOrCurrency) =>
		countryOrCurrency is { } text && ByName.TryGetValue(text.Trim(), out var code)
			? code
			: null;

	private static Dictionary<string, string> BuildNames()
	{
		var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (code, aliases) in ByCode)
		{
			names[code] = code;
			foreach (var alias in aliases)
				names[alias] = code;
		}
		return names;
	}
}
