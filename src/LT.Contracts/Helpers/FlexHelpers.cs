using System.Globalization;
using System.Text.RegularExpressions;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;

namespace LT.Contracts.Helpers;

/// <summary>Flex Web Service formats: IB's date / time strings and error codes.</summary>
public static partial class FlexHelpers
{
	// Flex query "Date Format" choices. dd/MM/yyyy is NOT accepted: it can't be told apart from
	// MM/dd/yyyy, so docs/ibkr-flex.md asks for yyyyMMdd (IB's default).
	private static readonly string[] DateFormats =
	[
		"yyyyMMdd",
		"yyyy-MM-dd",
		"MM/dd/yyyy",
		"MM/dd/yy",
		"dd-MMM-yy",
		"dd-MMM-yyyy",
		"yyyy/MM/dd",
	];

	private static readonly string[] TimeFormats = ["HHmmss", "HH:mm:ss", "HHmm", "HH:mm"];

	// date, then optionally a separator (; , space T) and a time, then optionally a zone
	[GeneratedRegex(
		@"^(?<date>[0-9A-Za-z/\-]+?)(?:[;, T]+(?<time>\d{1,2}:?\d{2}(?::?\d{2})?))?\s*(?<zone>[A-Za-z]{1,5}|[+-]\d{2}:?\d{2})?$"
	)]
	private static partial Regex DateTimePattern();

	/// <summary>Codes that mean "not ready yet / busy, ask again shortly".</summary>
	public static bool IsRetryable(int code) =>
		code is 1001 or 1004 or 1005 or 1006 or 1007 or 1008 or 1009 or 1018 or 1019 or 1021;

	/// <summary>A clear message for IB's Flex error <paramref name="code"/>.</summary>
	public static string GetErrorMessage(int code, string? ibMessage) =>
		code switch
		{
			1003 => FlexValidationCodes.FVC_012.GetValidationMessage(),
			1010 => FlexValidationCodes.FVC_013.GetValidationMessage(),
			1011 => FlexValidationCodes.FVC_010.GetValidationMessage(),
			1012 => FlexValidationCodes.FVC_005.GetValidationMessage(),
			1013 => FlexValidationCodes.FVC_007.GetValidationMessage(),
			1014 => FlexValidationCodes.FVC_008.GetValidationMessage(),
			1015 => FlexValidationCodes.FVC_006.GetValidationMessage(),
			1018 => FlexValidationCodes.FVC_009.GetValidationMessage(),
			1020 => FlexValidationCodes.FVC_011.GetValidationMessage(),
			_ => FlexValidationCodes.FVC_014.GetValidationMessage(code, ibMessage ?? string.Empty),
		};

	/// <summary>yyyyMMdd, the format of the fd / td override.</summary>
	public static string FormatRequestDate(DateOnly date) =>
		date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

	/// <summary>A Flex date (any of IB's date formats except dd/MM/yyyy); null when empty or unknown.</summary>
	public static DateOnly? ParseDate(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;
		return DateOnly.TryParseExact(
			value.Trim(),
			DateFormats,
			CultureInfo.InvariantCulture,
			DateTimeStyles.None,
			out var date
		)
			? date
			: null;
	}

	/// <summary>
	/// A Flex date-time ("20261026;093512", "2026-10-26, 09:35:12", "20261026" …): its date, and
	/// its moment when a time is present. Times without an offset are in <paramref name="zone"/>
	/// (US Eastern, FlexConstants.StatementTimeZoneId); a zone abbreviation (EDT) is ignored.
	/// </summary>
	public static (DateOnly? Date, DateTimeOffset? Time) ParseDateTime(
		string? value,
		TimeZoneInfo zone
	)
	{
		if (string.IsNullOrWhiteSpace(value))
			return (null, null);
		var text = value.Trim();
		// yyyyMMddHHmmss, no separator
		if (text.Length == 14 && text.All(char.IsAsciiDigit))
			text = $"{text[..8]};{text[8..]}";

		var match = DateTimePattern().Match(text);
		if (!match.Success)
			return (null, null);
		var date = ParseDate(match.Groups["date"].Value);
		if (date is not { } day || !match.Groups["time"].Success)
			return (date, null);
		if (
			!TimeOnly.TryParseExact(
				match.Groups["time"].Value,
				TimeFormats,
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out var time
			)
		)
			return (date, null);

		var local = day.ToDateTime(time);
		var zoneText = match.Groups["zone"].Value;
		var offset =
			zoneText.Length > 0 && (zoneText[0] == '+' || zoneText[0] == '-')
				? TimeSpan.ParseExact(
					zoneText.Replace(":", ""),
					["\\+hhmm", "\\-hhmm"],
					CultureInfo.InvariantCulture,
					zoneText[0] == '-' ? TimeSpanStyles.AssumeNegative : TimeSpanStyles.None
				)
				: zone.GetUtcOffset(local);
		return (date, new DateTimeOffset(local, offset));
	}

	/// <summary>A Flex number ("1,234.56", "-1.5"); null when empty or not a number.</summary>
	public static double? ParseDouble(string? value) =>
		double.TryParse(
			value?.Replace(",", ""),
			NumberStyles.Float,
			CultureInfo.InvariantCulture,
			out var number
		)
			? number
			: null;

	public static decimal? ParseDecimal(string? value) =>
		decimal.TryParse(
			value?.Replace(",", ""),
			NumberStyles.Float,
			CultureInfo.InvariantCulture,
			out var number
		)
			? number
			: null;

	/// <summary>The zone Flex times are in.</summary>
	public static TimeZoneInfo GetStatementTimeZone() =>
		IbTimeHelpers.FindTimeZone(FlexConstants.StatementTimeZoneId);
}
