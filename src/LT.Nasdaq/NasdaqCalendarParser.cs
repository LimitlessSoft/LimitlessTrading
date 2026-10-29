using System.Globalization;
using System.Net;
using System.Text.Json;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;

namespace LT.Nasdaq;

/// <summary>
/// Reads nasdaq.com's calendar JSON, as recorded on 2026-10-27 (earnings of a past day on
/// 2026-10-29). Every answer is
/// <c>{"data":…,"message":…,"status":{"rCode":200,"bCodeMessage":[{"code":…,"errorMessage":…}]}}</c>;
/// the rows are <c>data.rows</c> (dividends: <c>data.calendar.rows</c>), null on a day without
/// any, and a day Nasdaq has no record of answers <c>data: null</c> with business code 1002. An
/// answer in another shape throws instead of becoming an empty list.
/// </summary>
public static class NasdaqCalendarParser
{
	/// <summary>
	/// The events of one calendar answer; <paramref name="day"/> is the day they were asked for
	/// (earnings and economic rows carry no date). Splits keep their own effective dates.
	/// </summary>
	public static IReadOnlyList<CalendarEventDto> Parse(
		NasdaqCalendar calendar,
		string json,
		DateOnly day
	)
	{
		JsonDocument document;
		try
		{
			document = JsonDocument.Parse(json);
		}
		catch (JsonException)
		{
			throw Unreadable(json);
		}
		using (document)
		{
			var root = document.RootElement;
			if (
				root.ValueKind != JsonValueKind.Object
				|| (
					!root.TryGetProperty("data", out var data)
					&& !root.TryGetProperty("status", out _)
				)
			)
				throw Unreadable(json);

			if (GetBusinessError(root) is { } error)
			{
				if (error.Code == NasdaqConstants.NoRecordCode)
					return [];
				throw new NasdaqServiceException(
					CalendarValidationCodes.CLVC_010.GetValidationMessage(error.Message)
				);
			}
			if (data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
				return [];

			var holder = data;
			if (calendar == NasdaqCalendar.Dividends)
			{
				if (!data.TryGetProperty("calendar", out holder))
					throw Unreadable(json);
				if (holder.ValueKind == JsonValueKind.Null)
					return [];
			}
			if (
				holder.ValueKind != JsonValueKind.Object
				|| !holder.TryGetProperty("rows", out var rows)
			)
				throw Unreadable(json);
			if (rows.ValueKind == JsonValueKind.Null)
				return [];
			if (rows.ValueKind != JsonValueKind.Array)
				throw Unreadable(json);

			var items = rows.EnumerateArray()
				.Where(x => x.ValueKind == JsonValueKind.Object)
				.ToList();
			var events = items
				.Select(x => ReadRow(calendar, x, day))
				.OfType<CalendarEventDto>()
				.ToList();
			if (items.Count > 0 && events.Count == 0)
				throw Unreadable(json);
			return events;
		}
	}

	/// <summary>"time-pre-market" / "time-after-hours"; "time-not-supplied" and others are unknown.</summary>
	public static CalendarEventTime ParseEarningsTime(string? text) =>
		text?.Trim().ToLowerInvariant() switch
		{
			"time-pre-market" => CalendarEventTime.BeforeOpen,
			"time-after-hours" => CalendarEventTime.AfterClose,
			_ => CalendarEventTime.Unknown,
		};

	/// <summary>A figure cell: HTML entities decoded (Nasdaq sends "&amp;nbsp;" for none), trimmed.</summary>
	public static string CleanCell(string? text) =>
		WebUtility.HtmlDecode(text ?? string.Empty).Replace(' ', ' ').Trim();

	private static CalendarEventDto? ReadRow(
		NasdaqCalendar calendar,
		JsonElement row,
		DateOnly day
	) =>
		calendar switch
		{
			NasdaqCalendar.Earnings => ReadEarnings(row, day),
			NasdaqCalendar.Dividends => ReadDividend(row, day),
			NasdaqCalendar.Splits => ReadSplit(row),
			NasdaqCalendar.Economic => ReadEconomic(row, day),
			_ => null,
		};

	// {"time":"time-after-hours","symbol":"V","name":"Visa Inc.","epsForecast":"$3.43",…}; a past
	// day's rows also have the result, {"eps":"$4.89","surprise":"3.82",…} ("($0.01)" negative,
	// "N/A" no surprise), today's and later ones don't (lastYearEPS instead)
	private static CalendarEventDto? ReadEarnings(JsonElement row, DateOnly day)
	{
		var symbol = GetText(row, "symbol");
		if (symbol.Length == 0)
			return null;
		var estimate = CalendarHelpers.ParseFigure(CleanCell(GetText(row, "epsForecast")));
		var actual = CalendarHelpers.ParseFigure(CleanCell(GetText(row, "eps")));
		var surprise = actual is null
			? null
			: CalendarHelpers.ParseFigure(CleanCell(GetText(row, "surprise")));
		return new CalendarEventDto
		{
			Date = day,
			Kind = CalendarEventKind.Earnings,
			TypeName = "Earnings",
			Symbol = symbol.ToUpperInvariant(),
			Company = GetText(row, "name"),
			Time = ParseEarningsTime(GetText(row, "time")),
			Detail = CalendarHelpers.FormatEarningsDetail(estimate, actual, surprise),
			EpsEstimate = estimate,
			EpsActual = actual,
			EpsSurprisePercent = surprise,
			Source = CalendarSource.Nasdaq,
		};
	}

	// {"companyName":"ASML Holding …","symbol":"ASML","dividend_Ex_Date":"10/27/2026","payment_Date":"11/04/2026","dividend_Rate":2.107668,…}
	private static CalendarEventDto? ReadDividend(JsonElement row, DateOnly day)
	{
		var symbol = GetText(row, "symbol");
		if (symbol.Length == 0)
			return null;
		var rate = decimal.TryParse(
			GetText(row, "dividend_Rate"),
			NumberStyles.Float,
			CultureInfo.InvariantCulture,
			out var value
		)
			? value.ToString("0.####", CultureInfo.InvariantCulture)
			: string.Empty;
		var payment = CalendarHelpers.ParseDate(GetText(row, "payment_Date"));
		var detail = string.Join(
			" · ",
			new[]
			{
				rate,
				payment is { } paid
					? "pays " + paid.ToString("d MMM", CultureInfo.InvariantCulture)
					: string.Empty,
			}.Where(x => x.Length > 0)
		);
		return new CalendarEventDto
		{
			Date = CalendarHelpers.ParseDate(GetText(row, "dividend_Ex_Date")) ?? day,
			Kind = CalendarEventKind.Dividend,
			TypeName = "Ex-dividend",
			Symbol = symbol.ToUpperInvariant(),
			Company = GetText(row, "companyName"),
			Detail = detail,
			Source = CalendarSource.Nasdaq,
		};
	}

	// {"symbol":"SOXX","name":"iShares PHLX SOX …","ratio":"3:1","executionDate":"11/05/2026"}
	private static CalendarEventDto? ReadSplit(JsonElement row)
	{
		var symbol = GetText(row, "symbol");
		if (
			symbol.Length == 0
			|| CalendarHelpers.ParseDate(GetText(row, "executionDate")) is not { } date
		)
			return null;
		return new CalendarEventDto
		{
			Date = date,
			Kind = CalendarEventKind.Split,
			TypeName = "Split",
			Symbol = symbol.ToUpperInvariant(),
			Company = GetText(row, "name"),
			Detail = string.Join(
				':',
				GetText(row, "ratio").Split(':', StringSplitOptions.TrimEntries)
			),
			Source = CalendarSource.Nasdaq,
		};
	}

	// {"gmt":"08:30","country":"United States","eventName":"Chicago Fed National Activity","actual":"0.07","consensus":"-0.10","previous":"-0.08",…}
	private static CalendarEventDto? ReadEconomic(JsonElement row, DateOnly day)
	{
		var name = CleanCell(GetText(row, "eventName"));
		if (name.Length == 0)
			return null;
		var time = CleanCell(GetText(row, "gmt"));
		var figures = new[]
		{
			("Act.", CleanCell(GetText(row, "actual"))),
			("Cons.", CleanCell(GetText(row, "consensus"))),
			("Prev.", CleanCell(GetText(row, "previous"))),
		};
		return new CalendarEventDto
		{
			Date = day,
			Kind = CalendarEventKind.Economic,
			TypeName = CleanCell(GetText(row, "country")),
			Company = name,
			TimeText = time.Equals("All Day", StringComparison.OrdinalIgnoreCase)
				? "All day"
				: time,
			Detail = string.Join(
				" · ",
				figures.Where(x => x.Item2.Length > 0).Select(x => $"{x.Item1} {x.Item2}")
			),
			Source = CalendarSource.Nasdaq,
		};
	}

	private static (int Code, string Message)? GetBusinessError(JsonElement root)
	{
		if (
			!root.TryGetProperty("status", out var status)
			|| status.ValueKind != JsonValueKind.Object
		)
			return null;
		if (
			status.TryGetProperty("bCodeMessage", out var codes)
			&& codes.ValueKind == JsonValueKind.Array
			&& codes.EnumerateArray().FirstOrDefault(x => x.ValueKind == JsonValueKind.Object)
				is { ValueKind: JsonValueKind.Object } first
		)
			return (
				first.TryGetProperty("code", out var code) && code.TryGetInt32(out var value)
					? value
					: 0,
				GetText(first, "errorMessage")
			);
		if (
			status.TryGetProperty("rCode", out var rCode)
			&& rCode.TryGetInt32(out var httpCode)
			&& httpCode != 200
		)
			return (httpCode, $"status {httpCode}");
		return null;
	}

	private static string GetText(JsonElement element, string name)
	{
		if (!element.TryGetProperty(name, out var value))
			return string.Empty;
		return value.ValueKind switch
		{
			JsonValueKind.String => value.GetString()?.Trim() ?? string.Empty,
			JsonValueKind.Number => value.GetRawText(),
			_ => string.Empty,
		};
	}

	private static NasdaqServiceException Unreadable(string json) =>
		new(
			CalendarValidationCodes.CLVC_009.GetValidationMessage(CalendarHelpers.GetSnippet(json))
		);
}
