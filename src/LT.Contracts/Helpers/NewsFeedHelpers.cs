using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Feeds;
using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>The app's own news feeds: catalog lookups, URLs, items → headlines, ticker tagging.</summary>
public static partial class NewsFeedHelpers
{
	public static NewsFeedDefinitionDto? FindFeed(string providerCode) =>
		FeedNewsConstants.Feeds.FirstOrDefault(x => x.Code == providerCode);

	/// <summary>What the provider serves: history and articles always (the archive), live and the rest per its feeds.</summary>
	public static NewsSourceCapabilities GetCapabilities(NewsFeedDefinitionDto feed)
	{
		var capabilities =
			NewsSourceCapabilities.Live
			| NewsSourceCapabilities.History
			| NewsSourceCapabilities.Articles;
		if (feed.BroadUrls.Count > 0)
			capabilities |= NewsSourceCapabilities.BroadTape;
		if (feed.SymbolUrl != null || feed.TagsSymbols)
			capabilities |= NewsSourceCapabilities.BySymbol;
		return capabilities;
	}

	public static NewsProviderDto ToProvider(NewsFeedDefinitionDto feed) =>
		new()
		{
			SourceId = FeedNewsConstants.SourceId,
			Code = feed.Code,
			Name = feed.Name,
			Capabilities = GetCapabilities(feed),
			SelectedByDefault = feed.SelectedByDefault,
		};

	/// <summary>The broad feeds collected while the app runs, with or without a news widget: the default ones.</summary>
	public static IReadOnlyList<NewsFeedDefinitionDto> GetBackgroundFeeds() =>
		[.. FeedNewsConstants.Feeds.Where(x => x.SelectedByDefault && x.BroadUrls.Count > 0)];

	/// <summary>A ticker as the feeds and the SEC write it: upper case, a class after a dash (BRK B, BRK.B → BRK-B).</summary>
	public static string ToFeedSymbol(string symbol) =>
		symbol.Trim().ToUpperInvariant().Replace(' ', '-').Replace('.', '-');

	public static bool IsSameSymbol(string a, string b) =>
		a.Length > 0 && b.Length > 0 && ToFeedSymbol(a) == ToFeedSymbol(b);

	/// <summary>Whether the headline is about the ticker (asked for it, or tagged with it).</summary>
	public static bool IsAbout(NewsHeadlineDto headline, string symbol) =>
		IsSameSymbol(headline.Symbol, symbol) || headline.Symbols.Any(x => IsSameSymbol(x, symbol));

	/// <summary>
	/// The feed URL for a symbol: the ticker in it, for SEC the CIK (null when the SEC doesn't
	/// know the ticker) and the date to page back from.
	/// </summary>
	public static string? BuildSymbolUrl(
		NewsFeedDefinitionDto feed,
		string symbol,
		int? cik,
		DateTimeOffset? before
	)
	{
		if (feed.SymbolUrl == null)
			return null;
		if (feed.Kind != NewsFeedKind.Sec)
			return string.Format(
				CultureInfo.InvariantCulture,
				feed.SymbolUrl,
				Uri.EscapeDataString(ToFeedSymbol(symbol))
			);
		if (cik is not { } known)
			return null;
		return string.Format(
			CultureInfo.InvariantCulture,
			feed.SymbolUrl,
			known.ToString("D10", CultureInfo.InvariantCulture),
			before?.UtcDateTime.ToString(
				FeedNewsConstants.SecDateFormat,
				CultureInfo.InvariantCulture
			) ?? string.Empty
		);
	}

	/// <summary>CIK → its tickers (a company can have several classes: GOOGL, GOOG).</summary>
	public static IReadOnlyDictionary<int, IReadOnlyList<string>> InvertTickerCiks(
		IReadOnlyDictionary<string, int> tickerCiks
	) =>
		tickerCiks
			.GroupBy(x => x.Value)
			.ToDictionary(
				x => x.Key,
				x => (IReadOnlyList<string>)[.. x.Select(y => y.Key).Order(StringComparer.Ordinal)]
			);

	/// <summary>
	/// Tickers a press release names with its US exchange: <c>(NYSE: BAC)</c>, <c>(Nasdaq: TMDX)</c>,
	/// <c>NYSE American: XYZ</c>; each once, in order.
	/// </summary>
	public static IReadOnlyList<string> ExtractTickers(string? text)
	{
		if (string.IsNullOrEmpty(text))
			return [];
		List<string> tickers = [];
		foreach (Match match in ExchangeTickerRegex().Matches(text))
		{
			var ticker = ToFeedSymbol(match.Groups["ticker"].Value);
			if (!tickers.Contains(ticker))
				tickers.Add(ticker);
		}
		return tickers;
	}

	/// <summary>
	/// An EDGAR entry title: "8-K - Shutterstock, Inc. (0001549346) (Filer)" (current filings)
	/// or "4  - Statement of changes …" (a company's feed: no company, the form's name).
	/// </summary>
	public static (string Form, string? Company, int? Cik, string? Description) ParseSecTitle(
		string title
	)
	{
		var match = SecTitleRegex().Match(title);
		if (!match.Success)
			return (title.Trim(), null, null, null);
		var form = match.Groups["form"].Value.Trim();
		var rest = match.Groups["rest"].Value.Trim();
		var company = SecCompanyRegex().Match(rest);
		return company.Success
			? (
				form,
				company.Groups["name"].Value.Trim(),
				int.Parse(company.Groups["cik"].Value, CultureInfo.InvariantCulture),
				null
			)
			: (form, null, null, rest.Length == 0 ? null : rest);
	}

	/// <summary>A routine form (<see cref="FeedNewsConstants.SecExcludedForms"/>, its amendment too).</summary>
	public static bool IsExcludedSecForm(string form)
	{
		var trimmed = form.Trim();
		if (trimmed.EndsWith("/A", StringComparison.OrdinalIgnoreCase))
			trimmed = trimmed[..^2];
		return FeedNewsConstants.SecExcludedForms.Contains(trimmed);
	}

	/// <summary>
	/// An archived SEC headline of a routine form (archived before they were left out): the form is
	/// the word, or the two words ("SC 13G"), before the first ": " of the built headline
	/// (<see cref="FormatSecHeadline"/>).
	/// </summary>
	public static bool IsExcludedSecHeadline(NewsHeadlineDto headline)
	{
		if (
			headline.SourceId != FeedNewsConstants.SourceId
			|| headline.ProviderCode != FeedNewsConstants.SecCode
		)
			return false;
		var colon = headline.Headline.IndexOf(": ", StringComparison.Ordinal);
		var words = (colon < 0 ? headline.Headline : headline.Headline[..colon]).Split(
			' ',
			StringSplitOptions.RemoveEmptyEntries
		);
		return words.Length > 0
			&& (
				IsExcludedSecForm(words[^1])
				|| (words.Length > 1 && IsExcludedSecForm($"{words[^2]} {words[^1]}"))
			);
	}

	/// <summary>
	/// Whether an item is recent enough to archive: within <paramref name="retentionDays"/> of
	/// <paramref name="now"/> (0 keeps everything), so a symbol feed listing years of filings
	/// doesn't refill days retention just deleted.
	/// </summary>
	public static bool IsWithinRetention(
		DateTimeOffset time,
		DateTimeOffset now,
		int retentionDays
	) => retentionDays <= 0 || time >= now.AddDays(-retentionDays);

	/// <summary>The 8-K item numbers an EDGAR summary lists ("Item 2.02: …").</summary>
	public static IReadOnlyList<string> GetSecItems(string? summary) =>
		summary == null
			? []
			: [.. SecItemRegex().Matches(summary).Select(x => x.Groups[1].Value).Distinct()];

	/// <summary>
	/// "Shutterstock, Inc. (SSTK) 8-K: Officers / directors, Reg FD" — the filer, its ticker, the
	/// form and what the items are about (9.01 exhibits left out); a company feed's entry is
	/// "AAPL 4: Statement of changes in beneficial ownership of securities".
	/// </summary>
	public static string FormatSecHeadline(
		string form,
		string? company,
		IReadOnlyList<string> tickers,
		IReadOnlyList<string> items,
		string? description
	)
	{
		var who = company switch
		{
			null => string.Join(", ", tickers),
			_ when tickers.Count > 0 => $"{company} ({string.Join(", ", tickers)})",
			_ => company,
		};
		var what = items
			.Where(x => x != FeedNewsConstants.SecExhibitItem)
			.Select(x => FeedNewsConstants.SecItemNames.TryGetValue(x, out var name) ? name : x)
			.ToList();
		var about = what.Count > 0 ? string.Join(", ", what) : description;
		var head = who.Length > 0 ? $"{who} {form}" : form;
		return about is { Length: > 0 } ? $"{head}: {about}" : head;
	}

	/// <summary>
	/// A feed item as a headline of the app's source. <paramref name="symbol"/>: the symbol whose
	/// feed it came from (tagged first). SEC entries get a built headline and their filer's
	/// tickers (<paramref name="cikTickers"/>); press releases the tickers they name. An item
	/// without a time gets <paramref name="now"/>; one without id or title, or a routine SEC form
	/// (<see cref="IsExcludedSecForm"/>), is dropped (null).
	/// </summary>
	public static NewsHeadlineDto? ToHeadline(
		NewsFeedDefinitionDto feed,
		FeedItemDto item,
		string? symbol,
		IReadOnlyDictionary<int, IReadOnlyList<string>>? cikTickers,
		DateTimeOffset now
	)
	{
		var id = item.Id.Length > 0 ? item.Id : item.Url ?? string.Empty;
		var title = CleanText(item.Title);
		if (id.Length == 0 || title.Length == 0)
			return null;

		List<string> symbols = [];
		if (symbol is { Length: > 0 })
			symbols.Add(ToFeedSymbol(symbol));
		var headline = title;
		if (feed.Kind == NewsFeedKind.Sec)
		{
			var (form, company, cik, description) = ParseSecTitle(title);
			if (IsExcludedSecForm(form))
				return null;
			if (
				cik is { } filer
				&& cikTickers != null
				&& cikTickers.TryGetValue(filer, out var filerTickers)
			)
				symbols.AddRange(filerTickers);
			headline = FormatSecHeadline(
				form,
				company,
				company == null ? symbols.Take(1).ToList() : [.. symbols.Distinct()],
				GetSecItems(item.Summary),
				description
			);
		}
		symbols.AddRange(item.Symbols.Where(x => !x.Contains(':')).Select(ToFeedSymbol));
		if (feed.TagsSymbols)
			symbols.AddRange(ExtractTickers($"{item.Title} {item.Summary}"));
		symbols = [.. symbols.Distinct()];

		return new NewsHeadlineDto
		{
			SourceId = FeedNewsConstants.SourceId,
			ProviderCode = feed.Code,
			ArticleId = id,
			Time = (item.Time ?? now).ToUniversalTime(),
			Headline = headline,
			Summary = TrimSummary(item.Summary),
			Url = item.Url,
			Symbol = symbols.FirstOrDefault() ?? string.Empty,
			Symbols = symbols,
		};
	}

	/// <summary>A summary for the archive: null when empty, cut to <see cref="FeedNewsConstants.MaxSummaryLength"/>.</summary>
	public static string? TrimSummary(string? summary)
	{
		if (string.IsNullOrWhiteSpace(summary))
			return null;
		var trimmed = summary.Trim();
		return trimmed.Length <= FeedNewsConstants.MaxSummaryLength
			? trimmed
			: trimmed[..FeedNewsConstants.MaxSummaryLength] + "…";
	}

	/// <summary>
	/// An EDGAR filing index page as article text: the form, its dates and items, and the list of
	/// documents; with the URL of its press release exhibit (EX-99.1) when it has one, to read too.
	/// </summary>
	public static (string Text, string? PressReleaseUrl) ParseSecFilingIndex(
		string html,
		string pageUrl
	)
	{
		var lines = new List<string>();
		if (SecFormNameRegex().Match(html) is { Success: true } formName)
			lines.Add(CleanText(TagRegex().Replace(formName.Groups[1].Value, " ")));
		foreach (Match info in SecInfoRegex().Matches(html))
		{
			var head = CleanText(info.Groups["head"].Value);
			var value = WebUtility.HtmlDecode(
				BreakRegex().Replace(info.Groups["value"].Value, "\n")
			);
			var values = value.Split('\n').Select(CleanText).Where(x => x.Length > 0).ToList();
			if (values.Count == 1)
				lines.Add($"{head}: {values[0]}");
			else if (values.Count > 1)
			{
				lines.Add($"{head}:");
				lines.AddRange(values.Select(x => $"  {x}"));
			}
		}

		string? pressRelease = null;
		var documents = new List<string>();
		foreach (Match row in TableRowRegex().Matches(html))
		{
			var cells = TableCellRegex()
				.Matches(row.Groups[1].Value)
				.Select(x => x.Groups[1].Value)
				.ToList();
			if (cells.Count < 4)
				continue;
			var description = CleanText(TagRegex().Replace(cells[1], " "));
			var type = CleanText(TagRegex().Replace(cells[3], " "));
			var href = HrefRegex().Match(cells[2]) is { Success: true } link
				? WebUtility.HtmlDecode(link.Groups[1].Value)
				: null;
			if (type.Length == 0 && description.Length == 0)
				continue;
			documents.Add(
				description.Length > 0 && description != type
					? $"  {type} — {description}"
					: $"  {type}"
			);
			if (
				pressRelease == null
				&& href != null
				&& FeedNewsConstants.SecPressReleaseTypes.Contains(
					type,
					StringComparer.OrdinalIgnoreCase
				)
				&& href.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)
			)
				pressRelease = new Uri(new Uri(pageUrl), href.Replace("/ix?doc=", "")).ToString();
		}
		if (documents.Count > 0)
		{
			lines.Add(string.Empty);
			lines.Add("Documents:");
			lines.AddRange(documents);
		}
		return (string.Join('\n', lines), pressRelease);
	}

	// Feed titles carry entities and stray line breaks
	private static string CleanText(string text) =>
		WhitespaceRegex().Replace(WebUtility.HtmlDecode(text), " ").Trim();

	[GeneratedRegex(
		@"\b(?:NYSE American|NYSE Arca|NYSE MKT|NYSE|NASDAQ(?:\s?(?:GS|GM|CM))?|Nasdaq(?:\s?(?:GS|GM|CM))?|AMEX|Cboe BZX|CBOE)\s*:\s*(?<ticker>[A-Z]{1,5}(?:[.\-][A-Z]{1,2})?)\b"
	)]
	private static partial Regex ExchangeTickerRegex();

	[GeneratedRegex(@"^(?<form>[^\s].*?)\s+-\s+(?<rest>.*)$")]
	private static partial Regex SecTitleRegex();

	[GeneratedRegex(@"^(?<name>.+?)\s*\((?<cik>\d{1,10})\)(?:\s*\([^)]*\))*$")]
	private static partial Regex SecCompanyRegex();

	[GeneratedRegex(@"Item\s+(\d+\.\d+)")]
	private static partial Regex SecItemRegex();

	[GeneratedRegex(
		@"<div\s+id=""formName"">(.*?)</div>",
		RegexOptions.IgnoreCase | RegexOptions.Singleline
	)]
	private static partial Regex SecFormNameRegex();

	[GeneratedRegex(
		@"<div\s+class=""infoHead"">(?<head>.*?)</div>\s*<div\s+class=""info"">(?<value>.*?)</div>",
		RegexOptions.IgnoreCase | RegexOptions.Singleline
	)]
	private static partial Regex SecInfoRegex();

	[GeneratedRegex(@"<tr[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
	private static partial Regex TableRowRegex();

	[GeneratedRegex(@"<td[^>]*>(.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
	private static partial Regex TableCellRegex();

	[GeneratedRegex(@"href=""([^""]+)""", RegexOptions.IgnoreCase)]
	private static partial Regex HrefRegex();

	[GeneratedRegex(@"<\s*br\s*/?\s*>", RegexOptions.IgnoreCase)]
	private static partial Regex BreakRegex();

	[GeneratedRegex(@"<[^>]*>")]
	private static partial Regex TagRegex();

	[GeneratedRegex(@"\s+")]
	private static partial Regex WhitespaceRegex();
}
