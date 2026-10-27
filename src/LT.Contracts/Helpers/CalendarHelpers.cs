using System.Globalization;
using System.Text;
using System.Text.Json;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>
/// The Calendar widget's logic: date ranges, which sources and requests a load needs, merging the
/// sources' events and grouping them by day, and IB's Wall Street Horizon data (the market-wide
/// request filter, reading WSH's JSON). IB's WSH answers have not been seen on the wire (no
/// subscription when recording), so that JSON is read leniently: events are found wherever an
/// array of objects sits, fields by the usual names, and an answer that can't be read is
/// reported (null) instead of becoming an empty list.
/// </summary>
public static class CalendarHelpers
{
	/// <summary>Earnings and board meetings: the two event types IB's own examples use.</summary>
	public static IReadOnlyList<CalendarEventTypeDto> FallbackEventTypes { get; } =
	[
		new()
		{
			Tag = CalendarConstants.EarningsTag,
			Name = "Earnings",
			Kind = CalendarEventKind.Earnings,
		},
		new()
		{
			Tag = CalendarConstants.BoardMeetingTag,
			Name = "Board meeting",
			Kind = CalendarEventKind.BoardMeeting,
		},
	];

	private static readonly string[] TypeKeys =
	[
		"event_type",
		"type",
		"tag",
		"event_tag",
		"code",
		"id",
		"key",
	];
	private static readonly string[] NameKeys =
	[
		"display_name",
		"name",
		"title",
		"label",
		"description",
	];
	private static readonly string[] SymbolKeys = ["ticker", "symbol", "tickers", "local_symbol"];
	private static readonly string[] CompanyKeys =
	[
		"company",
		"company_name",
		"companyname",
		"issuer",
		"issuer_name",
		"long_name",
	];
	private static readonly string[] ConIdKeys = ["conid", "con_id", "contract_id", "ib_conid"];
	private static readonly string[] DateKeys =
	[
		"index_date",
		"event_date",
		"date",
		"earnings_date",
		"announce_date",
		"start_date",
		"meeting_date",
	];
	private static readonly string[] TimeKeys =
	[
		"time_of_day",
		"earnings_time",
		"announce_time",
		"announcement_time",
		"time",
		"timing",
		"session",
	];
	private static readonly string[] EventNameKeys = ["event_name", "description", "title"];
	private static readonly string[] ListKeys = ["events", "data", "results", "items"];
	private static readonly string[] DateFormats =
	[
		"yyyyMMdd",
		"yyyy-MM-dd",
		"yyyy/MM/dd",
		"M/d/yyyy",
	];

	/// <summary>Today in New York, the day WSH's US-centred dates are counted in.</summary>
	public static DateOnly GetToday(DateTimeOffset now) =>
		TradeJournalHelpers.GetTradeDay(now, TradeJournalHelpers.GetTradeDayTimeZone());

	/// <summary>First and last day (inclusive) of <paramref name="range"/>; weeks start Monday.</summary>
	public static (DateOnly Start, DateOnly End) GetDates(CalendarRange range, DateOnly today)
	{
		var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
		return range switch
		{
			CalendarRange.ThisWeek => (monday, monday.AddDays(6)),
			CalendarRange.NextWeek => (monday.AddDays(7), monday.AddDays(13)),
			CalendarRange.ThisMonth => (
				new DateOnly(today.Year, today.Month, 1),
				new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month))
			),
			_ => throw new ArgumentOutOfRangeException(nameof(range), range, null),
		};
	}

	/// <summary>
	/// Every day from <paramref name="start"/> to <paramref name="end"/>, at most
	/// <see cref="CalendarConstants.MaxDaysPerLoad"/> of them.
	/// </summary>
	public static IReadOnlyList<DateOnly> GetDays(DateOnly start, DateOnly end)
	{
		var days = new List<DateOnly>();
		for (
			var day = start;
			day <= end && days.Count < CalendarConstants.MaxDaysPerLoad;
			day = day.AddDays(1)
		)
			days.Add(day);
		return days;
	}

	/// <summary>
	/// The type tags to ask WSH for: every known type, earnings only, or every other type. Falls
	/// back to <see cref="FallbackEventTypes"/>' tags when the metadata named none of that kind.
	/// </summary>
	public static IReadOnlyList<string> GetRequestTags(
		CalendarEventFilter filter,
		IReadOnlyList<CalendarEventTypeDto> types
	)
	{
		var known = types.Count > 0 ? types : FallbackEventTypes;
		var tags = known
			.Where(x => Matches(x.Kind, filter))
			.Select(x => x.Tag)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (tags.Count > 0)
			return tags;
		return [.. FallbackEventTypes.Where(x => Matches(x.Kind, filter)).Select(x => x.Tag)];
	}

	/// <summary>
	/// WSH's market-wide filter: every country, at most
	/// <see cref="CalendarConstants.MaxEventsPerRequest"/> events, each tag switched on — e.g.
	/// <c>{"country":"All","limit":100,"wshe_ed":"true"}</c> (the shape IB's examples use).
	/// </summary>
	public static string BuildEventFilter(IEnumerable<string> tags)
	{
		using var stream = new MemoryStream();
		using (var writer = new Utf8JsonWriter(stream))
		{
			writer.WriteStartObject();
			writer.WriteString("country", CalendarConstants.AllCountries);
			writer.WriteNumber("limit", CalendarConstants.MaxEventsPerRequest);
			foreach (var tag in tags)
				writer.WriteString(tag, "true");
			writer.WriteEndObject();
		}
		return Encoding.UTF8.GetString(stream.ToArray());
	}

	/// <summary>Whether an event of <paramref name="kind"/> passes <paramref name="filter"/>.</summary>
	public static bool Matches(CalendarEventKind kind, CalendarEventFilter filter) =>
		filter switch
		{
			CalendarEventFilter.Earnings => kind == CalendarEventKind.Earnings,
			CalendarEventFilter.Other => kind != CalendarEventKind.Earnings
				&& kind != CalendarEventKind.Economic,
			CalendarEventFilter.Economic => kind == CalendarEventKind.Economic,
			_ => true,
		};

	/// <summary>Whether <paramref name="filter"/> shows corporate events (WSH has nothing else).</summary>
	public static bool HasCorporateEvents(CalendarEventFilter filter) =>
		filter != CalendarEventFilter.Economic;

	/// <summary>Whether <paramref name="source"/> is switched on in Settings → Calendar.</summary>
	public static bool IsEnabled(CalendarSource source, AppSettingsDto settings) =>
		source switch
		{
			CalendarSource.WallStreetHorizon => settings.CalendarWallStreetHorizon,
			CalendarSource.Nasdaq => settings.CalendarNasdaq,
			_ => false,
		};

	/// <summary>The sources switched on in Settings → Calendar, in <see cref="CalendarSource"/> order.</summary>
	public static IReadOnlyList<CalendarSource> GetEnabledSources(AppSettingsDto settings) =>
		[.. Enum.GetValues<CalendarSource>().Where(x => IsEnabled(x, settings))];

	/// <summary>The Nasdaq calendars holding events that pass <paramref name="filter"/>.</summary>
	public static IReadOnlyList<NasdaqCalendar> GetNasdaqCalendars(CalendarEventFilter filter) =>
		filter switch
		{
			CalendarEventFilter.Earnings => [NasdaqCalendar.Earnings],
			CalendarEventFilter.Other => [NasdaqCalendar.Dividends, NasdaqCalendar.Splits],
			CalendarEventFilter.Economic => [NasdaqCalendar.Economic],
			_ =>
			[
				NasdaqCalendar.Earnings,
				NasdaqCalendar.Dividends,
				NasdaqCalendar.Splits,
				NasdaqCalendar.Economic,
			],
		};

	/// <summary>
	/// The Nasdaq requests a load of <paramref name="days"/> needs, in the order they are sent:
	/// splits once, from the first day (Nasdaq answers every split from that day on); then day by
	/// day each other calendar, earnings and dividends on weekdays only
	/// (<see cref="NasdaqConstants.WeekdayOnlyCalendars"/>). A week of everything is 18 requests,
	/// a month at most ~80.
	/// </summary>
	public static IReadOnlyList<(NasdaqCalendar Calendar, DateOnly Day)> GetNasdaqRequests(
		IReadOnlyList<DateOnly> days,
		CalendarEventFilter filter
	)
	{
		var calendars = GetNasdaqCalendars(filter);
		var requests = new List<(NasdaqCalendar, DateOnly)>();
		if (days.Count == 0)
			return requests;
		if (calendars.Contains(NasdaqCalendar.Splits))
			requests.Add((NasdaqCalendar.Splits, days[0]));
		foreach (var day in days)
		foreach (var calendar in calendars.Where(x => x != NasdaqCalendar.Splits))
			if (!IsWeekend(day) || !NasdaqConstants.WeekdayOnlyCalendars.Contains(calendar))
				requests.Add((calendar, day));
		return requests;
	}

	/// <summary>
	/// The <c>date</c> Nasdaq is asked for to get <paramref name="day"/>'s events: the day itself,
	/// the day after for the economic calendar (<see cref="NasdaqConstants.EconomicDateOffsetDays"/>).
	/// </summary>
	public static DateOnly GetNasdaqRequestDate(NasdaqCalendar calendar, DateOnly day) =>
		calendar == NasdaqCalendar.Economic
			? day.AddDays(NasdaqConstants.EconomicDateOffsetDays)
			: day;

	/// <summary>"IB Wall Street Horizon", "Nasdaq".</summary>
	public static string GetSourceName(CalendarSource source) =>
		source switch
		{
			CalendarSource.WallStreetHorizon => CalendarConstants.WallStreetHorizonSourceName,
			CalendarSource.Nasdaq => CalendarConstants.NasdaqSourceName,
			_ => source.ToString(),
		};

	private static bool IsWeekend(DateOnly day) =>
		day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

	/// <summary>Whether IB's error <paramref name="code"/> means "no WSH subscription".</summary>
	public static bool IsNotSubscribed(int code) =>
		CalendarConstants.NotSubscribedErrorCodes.Contains(code);

	/// <summary>What kind of event a type is, from its tag and name.</summary>
	public static CalendarEventKind GetKind(string tag, string name)
	{
		if (string.Equals(tag, CalendarConstants.EarningsTag, StringComparison.OrdinalIgnoreCase))
			return CalendarEventKind.Earnings;
		if (
			string.Equals(
				tag,
				CalendarConstants.BoardMeetingTag,
				StringComparison.OrdinalIgnoreCase
			)
		)
			return CalendarEventKind.BoardMeeting;
		var text = $"{tag} {name}".ToLowerInvariant();
		if (text.Contains("earning"))
			return CalendarEventKind.Earnings;
		if (text.Contains("dividend") || text.Contains("_div"))
			return CalendarEventKind.Dividend;
		if (text.Contains("split"))
			return CalendarEventKind.Split;
		if (text.Contains("conference") || text.Contains("presentation"))
			return CalendarEventKind.Conference;
		if (
			text.Contains("shareholder")
			|| text.Contains("stockholder")
			|| text.Contains("annual meeting")
			|| text.Contains("agm")
		)
			return CalendarEventKind.ShareholderMeeting;
		if (text.Contains("board"))
			return CalendarEventKind.BoardMeeting;
		return CalendarEventKind.Other;
	}

	/// <summary>"wshe_option_expiry" → "Option expiry", for types the metadata didn't name.</summary>
	public static string GetNameFromTag(string tag)
	{
		var text = tag.StartsWith(CalendarConstants.TagPrefix, StringComparison.OrdinalIgnoreCase)
			? tag[CalendarConstants.TagPrefix.Length..]
			: tag;
		text = text.Replace('_', ' ').Trim();
		return text.Length == 0 ? "Corporate event" : char.ToUpperInvariant(text[0]) + text[1..];
	}

	/// <summary>WSH's time of an event: BMO / AMC / DMH or the words, else unknown.</summary>
	public static CalendarEventTime ParseTime(string? text)
	{
		var value = text?.Trim().ToUpperInvariant() ?? string.Empty;
		if (value.Length == 0)
			return CalendarEventTime.Unknown;
		if (value.StartsWith("BMO") || value.Contains("BEFORE") || value.StartsWith("PRE"))
			return CalendarEventTime.BeforeOpen;
		if (value.StartsWith("AMC") || value.Contains("AFTER") || value.StartsWith("POST"))
			return CalendarEventTime.AfterClose;
		if (value.StartsWith("DMH") || value.Contains("DURING") || value.Contains("INTRADAY"))
			return CalendarEventTime.DuringMarket;
		return CalendarEventTime.Unknown;
	}

	/// <summary>"Before open", "After close" …, or WSH's own text, or empty.</summary>
	public static string FormatTime(CalendarEventTime time, string timeText) =>
		time switch
		{
			CalendarEventTime.BeforeOpen => "Before open",
			CalendarEventTime.DuringMarket => "During market",
			CalendarEventTime.AfterClose => "After close",
			_ => timeText,
		};

	/// <summary>
	/// The event types WSH's metadata names: any object whose type field holds a <c>wshe_</c> tag
	/// (named by its name field), and any <c>wshe_</c> key holding an object or a name. Empty when
	/// the document names none or isn't JSON.
	/// </summary>
	public static IReadOnlyList<CalendarEventTypeDto> ParseEventTypes(string json)
	{
		var types = new Dictionary<string, CalendarEventTypeDto>(StringComparer.OrdinalIgnoreCase);
		try
		{
			using var document = JsonDocument.Parse(json);
			CollectEventTypes(document.RootElement, types, 0);
		}
		catch (JsonException)
		{
			return [];
		}
		return [.. types.Values];
	}

	/// <summary>
	/// The events of one WSH answer. Events without a date go on <paramref name="day"/>, the day
	/// asked for. Null when the answer can't be read: not JSON, no list of events in it, or a
	/// list none of whose entries names a symbol or company.
	/// </summary>
	public static IReadOnlyList<CalendarEventDto>? ParseEvents(
		string json,
		IReadOnlyList<CalendarEventTypeDto> types,
		DateOnly day
	)
	{
		if (string.IsNullOrWhiteSpace(json))
			return [];
		JsonDocument document;
		try
		{
			document = JsonDocument.Parse(json);
		}
		catch (JsonException)
		{
			return null;
		}
		using (document)
		{
			var items = FindEventObjects(document.RootElement, 0);
			if (items == null)
				return null;
			var byTag = new Dictionary<string, CalendarEventTypeDto>(
				StringComparer.OrdinalIgnoreCase
			);
			foreach (var type in types.Concat(FallbackEventTypes))
				byTag.TryAdd(type.Tag, type);
			var events = items.Select(x => ReadEvent(x, byTag, day)).OfType<CalendarEventDto>();
			var list = events.ToList();
			return items.Count > 0 && list.Count == 0 ? null : list;
		}
	}

	/// <summary>
	/// Events by day, earliest first (duplicates merged, see <see cref="Merge"/>); within a day the
	/// economic releases first by their time, then corporate events before open, during market,
	/// after close, without a time, then by symbol. Days in <paramref name="truncatedDays"/> are
	/// marked as possibly incomplete.
	/// </summary>
	public static IReadOnlyList<CalendarDayDto> GroupByDay(
		IEnumerable<CalendarEventDto> events,
		IReadOnlySet<DateOnly>? truncatedDays = null
	) =>
		[
			.. Merge(events)
				.GroupBy(x => x.Date)
				.OrderBy(x => x.Key)
				.Select(x => new CalendarDayDto
				{
					Date = x.Key,
					Events =
					[
						.. x.OrderBy(e => e.Kind == CalendarEventKind.Economic ? 0 : 1)
							.ThenBy(e => GetTimeOrder(e.Time))
							.ThenBy(e => GetTimeTextOrder(e.TimeText), StringComparer.Ordinal)
							.ThenBy(e => e.Kind)
							.ThenBy(e => e.Symbol, StringComparer.OrdinalIgnoreCase)
							.ThenBy(e => e.Company, StringComparer.OrdinalIgnoreCase),
					],
					IsTruncated = truncatedDays?.Contains(x.Key) == true,
				}),
		];

	/// <summary>
	/// One event per day, kind and symbol (and type, for <see cref="CalendarEventKind.Other"/>,
	/// which covers many types), in the order given: two sources reporting the same earnings date
	/// become one, the first source's, with blanks (company, conId, time, detail) filled from the
	/// others. Events without a symbol (economic releases) merge only when everything shown is the
	/// same: a release can be listed twice with different figures (Japan's Leading Index as a
	/// change and as a level). Never changes the events passed in.
	/// </summary>
	public static IReadOnlyList<CalendarEventDto> Merge(IEnumerable<CalendarEventDto> events)
	{
		var merged = new List<CalendarEventDto>();
		var byKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (var item in events)
		{
			var key = GetMergeKey(item);
			if (!byKey.TryGetValue(key, out var index))
			{
				byKey[key] = merged.Count;
				merged.Add(item);
				continue;
			}
			var kept = merged[index];
			if (!NeedsFill(kept, item))
				continue;
			var copy = Copy(kept);
			if (copy.Company.Length == 0)
				copy.Company = item.Company;
			if (copy.ConId == 0)
				copy.ConId = item.ConId;
			if (copy.Time == CalendarEventTime.Unknown && copy.TimeText.Length == 0)
			{
				copy.Time = item.Time;
				copy.TimeText = item.TimeText;
			}
			if (copy.Detail.Length == 0)
				copy.Detail = item.Detail;
			merged[index] = copy;
		}
		return merged;
	}

	private static string GetMergeKey(CalendarEventDto item) =>
		item.Symbol.Length > 0
			? string.Join(
				'\u001f',
				item.Date.DayNumber,
				(int)item.Kind,
				item.Symbol,
				item.Kind == CalendarEventKind.Other ? item.TypeName : string.Empty
			)
			: string.Join(
				'\u001f',
				item.Date.DayNumber,
				(int)item.Kind,
				string.Empty,
				item.TypeName,
				item.Company,
				item.TimeText,
				item.Detail
			);

	private static bool NeedsFill(CalendarEventDto kept, CalendarEventDto other) =>
		(kept.Company.Length == 0 && other.Company.Length > 0)
		|| (kept.ConId == 0 && other.ConId != 0)
		|| (
			kept.Time == CalendarEventTime.Unknown
			&& kept.TimeText.Length == 0
			&& (other.Time != CalendarEventTime.Unknown || other.TimeText.Length > 0)
		)
		|| (kept.Detail.Length == 0 && other.Detail.Length > 0);

	private static CalendarEventDto Copy(CalendarEventDto item) =>
		new()
		{
			Date = item.Date,
			Kind = item.Kind,
			TypeTag = item.TypeTag,
			TypeName = item.TypeName,
			Symbol = item.Symbol,
			Company = item.Company,
			ConId = item.ConId,
			Time = item.Time,
			TimeText = item.TimeText,
			Detail = item.Detail,
			Source = item.Source,
		};

	// "All day" before the clock times ("08:30" sorts as text); other texts after them
	private static string GetTimeTextOrder(string text) =>
		text.Length == 0 ? "~"
		: text.Equals("All day", StringComparison.OrdinalIgnoreCase) ? "0"
		: char.IsDigit(text[0]) ? "1" + text
		: "2" + text;

	/// <summary>The start of an answer for an error message, on one line.</summary>
	public static string GetSnippet(string text)
	{
		var line = string.Join(
			' ',
			text.Split((char[])['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries)
		);
		return line.Length <= CalendarConstants.UnreadableSnippetLength
			? line
			: line[..CalendarConstants.UnreadableSnippetLength] + "…";
	}

	private static int GetTimeOrder(CalendarEventTime time) =>
		time switch
		{
			CalendarEventTime.BeforeOpen => 0,
			CalendarEventTime.DuringMarket => 1,
			CalendarEventTime.AfterClose => 2,
			_ => 3,
		};

	private static void CollectEventTypes(
		JsonElement element,
		Dictionary<string, CalendarEventTypeDto> types,
		int depth
	)
	{
		if (depth > 8)
			return;
		if (element.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in element.EnumerateArray())
				CollectEventTypes(item, types, depth + 1);
			return;
		}
		if (element.ValueKind != JsonValueKind.Object)
			return;

		var tag = FirstString(element, TypeKeys, IsTag);
		if (tag != null)
			AddType(types, tag, FirstString(element, NameKeys, x => !IsTag(x)));
		foreach (var property in element.EnumerateObject())
		{
			if (IsTag(property.Name))
			{
				var name = property.Value.ValueKind switch
				{
					JsonValueKind.Object => FirstString(property.Value, NameKeys, x => !IsTag(x)),
					JsonValueKind.String when !IsFlag(property.Value.GetString()) =>
						property.Value.GetString(),
					_ => null,
				};
				AddType(types, property.Name, name);
			}
			CollectEventTypes(property.Value, types, depth + 1);
		}
	}

	private static void AddType(
		Dictionary<string, CalendarEventTypeDto> types,
		string tag,
		string? name
	)
	{
		if (types.ContainsKey(tag))
			return;
		var displayName = string.IsNullOrWhiteSpace(name) ? GetNameFromTag(tag) : name.Trim();
		types[tag] = new CalendarEventTypeDto
		{
			Tag = tag,
			Name = displayName,
			Kind = GetKind(tag, displayName),
		};
	}

	// The objects that are events: the root array's, the root itself, or the first list found
	private static List<JsonElement>? FindEventObjects(JsonElement element, int depth)
	{
		if (depth > 4)
			return null;
		if (element.ValueKind == JsonValueKind.Array)
			return [.. element.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object)];
		if (element.ValueKind != JsonValueKind.Object)
			return null;
		if (IsEventLike(element))
			return [element];

		var properties = element.EnumerateObject().ToList();
		if (properties.Count == 0)
			return [];
		var lists = properties
			.Where(x => x.Value.ValueKind == JsonValueKind.Array)
			.OrderBy(x => ListKeys.Contains(x.Name, StringComparer.OrdinalIgnoreCase) ? 0 : 1)
			.ToList();
		if (lists.Count > 0)
			return FindEventObjects(lists[0].Value, depth + 1);
		foreach (var property in properties.Where(x => x.Value.ValueKind == JsonValueKind.Object))
			if (FindEventObjects(property.Value, depth + 1) is { Count: > 0 } found)
				return found;
		return null;
	}

	private static bool IsEventLike(JsonElement element) =>
		FirstString(element, SymbolKeys, _ => true) != null
		|| FirstString(element, CompanyKeys, _ => true) != null
		|| FirstString(element, TypeKeys, IsTag) != null;

	private static CalendarEventDto? ReadEvent(
		JsonElement element,
		Dictionary<string, CalendarEventTypeDto> types,
		DateOnly day
	)
	{
		var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		Flatten(element, fields, 0);

		var symbol = First(fields, SymbolKeys) ?? string.Empty;
		var company = First(fields, CompanyKeys) ?? string.Empty;
		if (symbol.Length == 0 && company.Length == 0)
			return null;

		var tag =
			TypeKeys.Select(x => fields.GetValueOrDefault(x)).FirstOrDefault(x => IsTag(x))
			?? fields.Values.FirstOrDefault(IsTag)
			?? string.Empty;
		var eventName = First(fields, EventNameKeys) ?? string.Empty;
		var type = types.GetValueOrDefault(tag);
		var typeName =
			type?.Name
			?? (
				tag.Length > 0 ? GetNameFromTag(tag)
				: eventName.Length > 0 ? eventName
				: "Corporate event"
			);
		var timeText = First(fields, TimeKeys) ?? string.Empty;
		var time = ParseTime(timeText);
		return new CalendarEventDto
		{
			Date = GetDate(fields) ?? day,
			Kind = type?.Kind ?? GetKind(tag, eventName),
			TypeTag = tag,
			TypeName = typeName,
			Symbol = symbol.Trim().ToUpperInvariant(),
			Company = company.Trim(),
			ConId = int.TryParse(
				First(fields, ConIdKeys),
				NumberStyles.Integer,
				CultureInfo.InvariantCulture,
				out var conId
			)
				? conId
				: 0,
			Time = time,
			TimeText = time == CalendarEventTime.Unknown ? timeText.Trim() : string.Empty,
			Source = CalendarSource.WallStreetHorizon,
		};
	}

	// The event's own values first, then those of objects nested in it (WSH's "data" …)
	private static void Flatten(JsonElement element, Dictionary<string, string> fields, int depth)
	{
		var nested = new List<JsonElement>();
		foreach (var property in element.EnumerateObject())
		{
			var value = property.Value;
			switch (value.ValueKind)
			{
				case JsonValueKind.String:
					fields.TryAdd(property.Name, value.GetString() ?? string.Empty);
					break;
				case JsonValueKind.Number:
					fields.TryAdd(property.Name, value.GetRawText());
					break;
				case JsonValueKind.Array:
					var first = value
						.EnumerateArray()
						.FirstOrDefault(x => x.ValueKind == JsonValueKind.String);
					if (first.ValueKind == JsonValueKind.String)
						fields.TryAdd(property.Name, first.GetString() ?? string.Empty);
					break;
				case JsonValueKind.Object:
					nested.Add(value);
					break;
			}
		}
		if (depth < 2)
			foreach (var value in nested)
				Flatten(value, fields, depth + 1);
	}

	private static DateOnly? GetDate(Dictionary<string, string> fields)
	{
		foreach (var key in DateKeys)
			if (fields.TryGetValue(key, out var value) && ParseDate(value) is { } date)
				return date;
		foreach (var (key, value) in fields)
			if (
				key.Contains("date", StringComparison.OrdinalIgnoreCase)
				&& ParseDate(value) is { } date
			)
				return date;
		return null;
	}

	/// <summary><c>20261028</c>, <c>2026-10-28</c> (also with a time after it), <c>10/28/2026</c>.</summary>
	public static DateOnly? ParseDate(string? text)
	{
		var value = text?.Trim() ?? string.Empty;
		if (value.Length > 10 && value[4] == '-' && (value[10] == 'T' || value[10] == ' '))
			value = value[..10];
		return DateOnly.TryParseExact(
			value,
			DateFormats,
			CultureInfo.InvariantCulture,
			DateTimeStyles.None,
			out var date
		)
			? date
			: null;
	}

	private static string? First(Dictionary<string, string> fields, string[] keys) =>
		keys.Select(x => fields.GetValueOrDefault(x))
			.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

	private static string? FirstString(
		JsonElement element,
		string[] keys,
		Func<string?, bool> accept
	)
	{
		foreach (var key in keys)
		foreach (var property in element.EnumerateObject())
			if (
				string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase)
				&& property.Value.ValueKind == JsonValueKind.String
				&& property.Value.GetString() is { Length: > 0 } value
				&& accept(value)
			)
				return value;
		return null;
	}

	private static bool IsTag(string? value) =>
		value != null
		&& value.StartsWith(CalendarConstants.TagPrefix, StringComparison.OrdinalIgnoreCase);

	private static bool IsFlag(string? value) =>
		value is null
		|| value.Equals("true", StringComparison.OrdinalIgnoreCase)
		|| value.Equals("false", StringComparison.OrdinalIgnoreCase);
}
