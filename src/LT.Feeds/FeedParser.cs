using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using LT.Contracts.Dtos.Feeds;

namespace LT.Feeds;

/// <summary>
/// RSS 2.0 and Atom with <see cref="XDocument"/> (no package). The XML declaration's encoding is
/// honoured (EDGAR's Atom is ISO-8859-1). Dates come as RFC 822 with numeric offsets or zone
/// names (EDT, GMT, UT) or as ISO 8601. An item missing its time keeps null; one missing title
/// and id is skipped.
/// </summary>
public static partial class FeedParser
{
	private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
	private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
	private static readonly XNamespace Content = "http://purl.org/rss/1.0/modules/content/";
	private static readonly XNamespace SeekingAlpha = "https://seekingalpha.com/api/1.0";

	// RFC 822 zone names feeds use instead of an offset
	private static readonly Dictionary<string, int> ZoneHours = new(
		StringComparer.OrdinalIgnoreCase
	)
	{
		["UT"] = 0,
		["UTC"] = 0,
		["GMT"] = 0,
		["Z"] = 0,
		["EST"] = -5,
		["EDT"] = -4,
		["CST"] = -6,
		["CDT"] = -5,
		["MST"] = -7,
		["MDT"] = -6,
		["PST"] = -8,
		["PDT"] = -7,
	};

	private static readonly string[] Months =
	[
		"jan",
		"feb",
		"mar",
		"apr",
		"may",
		"jun",
		"jul",
		"aug",
		"sep",
		"oct",
		"nov",
		"dec",
	];

	/// <exception cref="XmlException">Not XML, or neither RSS nor Atom.</exception>
	public static FeedResponseDto Parse(Stream stream)
	{
		var settings = new XmlReaderSettings
		{
			DtdProcessing = DtdProcessing.Ignore,
			XmlResolver = null,
		};
		using var reader = XmlReader.Create(stream, settings);
		return Parse(XDocument.Load(reader));
	}

	public static FeedResponseDto Parse(string xml) =>
		Parse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)));

	public static FeedResponseDto Parse(XDocument document)
	{
		var root = document.Root ?? throw new XmlException("The feed is empty.");
		if (root.Name == Atom + "feed")
			return new FeedResponseDto
			{
				Title = GetAtomFeedTitle(root),
				Items =
				[
					.. root.Elements(Atom + "entry").Select(ParseAtomEntry).OfType<FeedItemDto>(),
				],
			};
		var channel =
			root.Name.LocalName == "rss"
				? root.Element("channel")
				: throw new XmlException($"Not an RSS or Atom feed ({root.Name.LocalName}).");
		if (channel == null)
			throw new XmlException("The RSS feed has no channel.");
		return new FeedResponseDto
		{
			Title = Text(channel.Element("title")),
			Items = [.. channel.Elements("item").Select(ParseRssItem).OfType<FeedItemDto>()],
		};
	}

	/// <summary>RFC 822 ("Thu, 29 Oct 2026 21:03:17 GMT", "… 12:58:19 EDT", "… 20:54 +0000") or ISO 8601; null when neither.</summary>
	public static DateTimeOffset? ParseDate(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return null;
		var value = text.Trim();
		var match = Rfc822Regex().Match(value);
		if (match.Success)
		{
			var month = Array.IndexOf(Months, match.Groups["month"].Value[..3].ToLowerInvariant());
			if (month < 0)
				return null;
			var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
			if (year < 100)
				year += 2000;
			var offset = ParseZone(match.Groups["zone"].Value);
			if (offset == null)
				return null;
			try
			{
				return new DateTimeOffset(
					year,
					month + 1,
					int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture),
					int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture),
					int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture),
					match.Groups["second"].Success
						? int.Parse(match.Groups["second"].Value, CultureInfo.InvariantCulture)
						: 0,
					offset.Value
				);
			}
			catch (ArgumentOutOfRangeException)
			{
				return null;
			}
		}
		return DateTimeOffset.TryParse(
			value,
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal,
			out var iso
		)
			? iso
			: null;
	}

	private static TimeSpan? ParseZone(string zone)
	{
		if (zone.Length == 0)
			return TimeSpan.Zero;
		if (zone[0] is '+' or '-')
		{
			var hours = int.Parse(zone.AsSpan(1, 2), CultureInfo.InvariantCulture);
			var minutes = int.Parse(zone.AsSpan(3, 2), CultureInfo.InvariantCulture);
			var offset = new TimeSpan(hours, minutes, 0);
			return zone[0] == '-' ? -offset : offset;
		}
		return ZoneHours.TryGetValue(zone, out var named) ? TimeSpan.FromHours(named) : null;
	}

	private static FeedItemDto? ParseRssItem(XElement item)
	{
		var title = Text(item.Element("title"));
		var url = Text(item.Element("link"));
		var id = Text(item.Element("guid")) ?? url;
		if (id == null || title == null)
			return null;
		return new FeedItemDto
		{
			Id = id,
			Title = title,
			Url = url,
			Summary = Text(item.Element("description")) ?? Text(item.Element(Content + "encoded")),
			Time = ParseDate(Text(item.Element("pubDate")) ?? Text(item.Element(Dc + "date"))),
			Categories = [.. item.Elements("category").Select(Text).OfType<string>()],
			Symbols =
			[
				.. item.Elements(SeekingAlpha + "stock")
					.Select(x => Text(x.Element(SeekingAlpha + "symbol")))
					.OfType<string>()
					.Distinct(),
			],
		};
	}

	private static FeedItemDto? ParseAtomEntry(XElement entry)
	{
		var title = Text(entry.Element(Atom + "title"));
		var links = entry.Elements(Atom + "link").ToList();
		var url =
			links
				.FirstOrDefault(x => (string?)x.Attribute("rel") is null or "alternate")
				?.Attribute("href")
				?.Value
			// EDGAR company feeds also name the filing in their content
			?? Text(
				entry
					.Element(Atom + "content")
					?.Elements()
					.FirstOrDefault(x => x.Name.LocalName == "filing-href")
			);
		var id = Text(entry.Element(Atom + "id")) ?? url;
		if (id == null || title == null)
			return null;
		var content = entry.Element(Atom + "content");
		return new FeedItemDto
		{
			Id = id,
			Title = title,
			Url = url,
			Summary =
				Text(entry.Element(Atom + "summary"))
				?? (content != null && !content.HasElements ? Text(content) : null),
			Time = ParseDate(
				Text(entry.Element(Atom + "published")) ?? Text(entry.Element(Atom + "updated"))
			),
			Categories =
			[
				.. entry
					.Elements(Atom + "category")
					.Select(x => (string?)x.Attribute("term") ?? Text(x))
					.OfType<string>(),
			],
		};
	}

	// EDGAR company feeds: the company's name, else the feed's title
	private static string? GetAtomFeedTitle(XElement feed) =>
		Text(feed.Element(Atom + "company-info")?.Element(Atom + "conformed-name"))
		?? Text(feed.Element(Atom + "title"));

	private static string? Text(XElement? element) =>
		element?.Value.Trim() is { Length: > 0 } text ? text : null;

	[GeneratedRegex(
		@"^(?:[A-Za-z]{3,9},?\s+)?(?<day>\d{1,2})\s+(?<month>[A-Za-z]{3,9})\s+(?<year>\d{2,4})\s+(?<hour>\d{1,2}):(?<minute>\d{2})(?::(?<second>\d{2}))?(?:\.\d+)?\s*(?<zone>[+-]\d{4}|[A-Za-z]{1,5})?$"
	)]
	private static partial Regex Rfc822Regex();
}
