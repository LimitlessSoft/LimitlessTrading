namespace LT.Contracts.Constants;

public static class IbTimeZoneConstants
{
	/// <summary>
	/// Old zone names IB still sends (contract details' timeZoneId) and the IANA zone each stands
	/// for. Systems without tzdata's backward links (e.g. Debian 13 without tzdata-legacy) don't
	/// know the old names.
	/// </summary>
	public static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<
		string,
		string
	>(StringComparer.OrdinalIgnoreCase)
	{
		["US/Eastern"] = "America/New_York",
		["US/Central"] = "America/Chicago",
		["US/Mountain"] = "America/Denver",
		["US/Pacific"] = "America/Los_Angeles",
		["US/Arizona"] = "America/Phoenix",
		["US/Alaska"] = "America/Anchorage",
		["US/Hawaii"] = "Pacific/Honolulu",
		["EST5EDT"] = "America/New_York",
		["CST6CDT"] = "America/Chicago",
		["MST7MDT"] = "America/Denver",
		["PST8PDT"] = "America/Los_Angeles",
		["EST"] = "America/Panama",
		["MST"] = "America/Phoenix",
		["Canada/Eastern"] = "America/Toronto",
		["Canada/Central"] = "America/Winnipeg",
		["Canada/Mountain"] = "America/Edmonton",
		["Canada/Pacific"] = "America/Vancouver",
		["Mexico/General"] = "America/Mexico_City",
		["Brazil/East"] = "America/Sao_Paulo",
		["GB"] = "Europe/London",
		["GB-Eire"] = "Europe/London",
		["Eire"] = "Europe/Dublin",
		["WET"] = "Europe/Lisbon",
		["CET"] = "Europe/Brussels",
		["MET"] = "Europe/Brussels",
		["EET"] = "Europe/Athens",
		["Europe/Kiev"] = "Europe/Kyiv",
		["Turkey"] = "Europe/Istanbul",
		["Israel"] = "Asia/Jerusalem",
		["Asia/Calcutta"] = "Asia/Kolkata",
		["Japan"] = "Asia/Tokyo",
		["Hongkong"] = "Asia/Hong_Kong",
		["Singapore"] = "Asia/Singapore",
		["PRC"] = "Asia/Shanghai",
		["ROC"] = "Asia/Taipei",
		["ROK"] = "Asia/Seoul",
		["Australia/NSW"] = "Australia/Sydney",
		["Australia/ACT"] = "Australia/Sydney",
		["Australia/Victoria"] = "Australia/Melbourne",
		["Australia/Queensland"] = "Australia/Brisbane",
		["Australia/West"] = "Australia/Perth",
		["Australia/South"] = "Australia/Adelaide",
		["NZ"] = "Pacific/Auckland",
		["GMT"] = "Etc/UTC",
		["UTC"] = "Etc/UTC",
		["Universal"] = "Etc/UTC",
	};
}
