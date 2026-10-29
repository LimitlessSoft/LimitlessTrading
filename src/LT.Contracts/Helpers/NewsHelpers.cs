using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

public static partial class NewsHelpers
{
	/// <summary>
	/// Reads IB's headline metadata, key / value pairs separated by colons, with or without the
	/// braces history wraps it in: <c>A:800015:L:en:K:-0.97:C:0.97</c> (L language, K sentiment,
	/// C confidence; K and C are not always there, a trailing flag like <c>E</c> is ignored).
	/// </summary>
	public static NewsExtraDataDto ParseExtraData(string? extraData)
	{
		var result = new NewsExtraDataDto();
		var parts = (extraData ?? string.Empty).Trim().Trim('{', '}').Split(':');
		for (var i = 0; i + 1 < parts.Length; i += 2)
		{
			var value = parts[i + 1].Trim();
			switch (parts[i].Trim())
			{
				case "L" when value.Length > 0:
					result.Language = value;
					break;
				case "K":
					result.Sentiment = ParseNumber(value);
					break;
				case "C":
					result.Confidence = ParseNumber(value);
					break;
			}
		}
		return result;
	}

	/// <summary>Identifies a headline across history, live replays and sources.</summary>
	public static string GetKey(NewsHeadlineDto headline) =>
		$"{headline.SourceId}|{headline.ProviderCode}|{headline.ArticleId}";

	/// <summary>"ibkr:BRFG", how a widget saves a provider selection.</summary>
	public static string GetProviderKey(NewsProviderDto provider) =>
		GetProviderKey(provider.SourceId, provider.Code);

	public static string GetProviderKey(string sourceId, string providerCode) =>
		$"{sourceId}{NewsConstants.ProviderKeySeparator}{providerCode}";

	/// <summary>Adds the headline's provider key and tickers (feed style) to an index of a day's headlines.</summary>
	public static void AddToIndex(
		ISet<string> symbols,
		ISet<string> providerKeys,
		NewsHeadlineDto headline
	)
	{
		providerKeys.Add(GetProviderKey(headline.SourceId, headline.ProviderCode));
		if (headline.Symbol.Length > 0)
			symbols.Add(NewsFeedHelpers.ToFeedSymbol(headline.Symbol));
		foreach (var symbol in headline.Symbols)
			symbols.Add(NewsFeedHelpers.ToFeedSymbol(symbol));
	}

	/// <summary>
	/// Where a news source's items come from, as a widget category (the same colors as the frame
	/// corner markers): the app's own feeds are <see cref="WidgetCategory.Tools"/>, everything else
	/// (IB) <see cref="WidgetCategory.Ibkr"/>.
	/// </summary>
	public static WidgetCategory GetSourceCategory(string sourceId) =>
		sourceId == FeedNewsConstants.SourceId ? WidgetCategory.Tools : WidgetCategory.Ibkr;

	/// <summary>Codes of the providers that belong to <paramref name="sourceId"/>.</summary>
	public static IReadOnlyList<string> GetProviderCodes(
		IEnumerable<NewsProviderDto> providers,
		string sourceId
	) => [.. providers.Where(x => x.SourceId == sourceId).Select(x => x.Code).Distinct()];

	/// <summary>
	/// Both lists as one, newest first, each headline once (the first seen wins), at most
	/// <paramref name="max"/>.
	/// </summary>
	public static List<NewsHeadlineDto> Merge(
		IEnumerable<NewsHeadlineDto> first,
		IEnumerable<NewsHeadlineDto> second,
		int max
	)
	{
		var keys = new HashSet<string>();
		return first
			.Concat(second)
			.Where(x => keys.Add(GetKey(x)))
			.OrderByDescending(x => x.Time)
			.Take(max)
			.ToList();
	}

	/// <summary>
	/// Puts a headline into a newest-first list at its time (after headlines of the same time).
	/// Returns its index, or -1 when <paramref name="keys"/> already has it.
	/// </summary>
	public static int Insert(
		List<NewsHeadlineDto> newestFirst,
		ISet<string> keys,
		NewsHeadlineDto headline
	)
	{
		if (!keys.Add(GetKey(headline)))
			return -1;
		var index = GetInsertIndex(newestFirst, x => x.Time, headline.Time);
		newestFirst.Insert(index, headline);
		return index;
	}

	/// <summary>Index in a newest-first list where an item of <paramref name="time"/> goes.</summary>
	public static int GetInsertIndex<T>(
		IReadOnlyList<T> newestFirst,
		Func<T, DateTimeOffset> getTime,
		DateTimeOffset time
	)
	{
		int low = 0,
			high = newestFirst.Count;
		while (low < high)
		{
			var middle = (low + high) / 2;
			if (getTime(newestFirst[middle]) >= time)
				low = middle + 1;
			else
				high = middle;
		}
		return low;
	}

	/// <summary>
	/// A headline's words for spotting the same story from two sources: letters and digits only,
	/// lower case, one space between words ("Aptar Reports Q3 Results" = "APTAR REPORTS Q3
	/// RESULTS."). Empty when it has none.
	/// </summary>
	public static string GetStoryKey(NewsHeadlineDto headline) =>
		string.Join(
			' ',
			NonWordRegex().Split(headline.Headline.ToLowerInvariant()).Where(x => x.Length > 0)
		);

	/// <summary>
	/// Whether the same story (<see cref="GetStoryKey"/>) was already seen within
	/// <paramref name="window"/> of the headline's time; if not, it is remembered in
	/// <paramref name="stories"/> (story key → times seen). Headlines of one provider are never
	/// each other's repeat (a provider repeating a headline means a new item, e.g. "Form 4").
	/// </summary>
	public static bool IsRepeatedStory(
		IDictionary<string, List<(DateTimeOffset Time, string Provider)>> stories,
		NewsHeadlineDto headline,
		TimeSpan window
	)
	{
		var key = GetStoryKey(headline);
		if (key.Length == 0)
			return false;
		var provider = $"{headline.SourceId}|{headline.ProviderCode}";
		if (!stories.TryGetValue(key, out var seen))
			stories[key] = seen = [];
		else if (
			seen.Any(x => x.Provider != provider && (x.Time - headline.Time).Duration() <= window)
		)
			return true;
		seen.Add((headline.Time, provider));
		return false;
	}

	/// <summary>Forgets a headline <see cref="IsRepeatedStory"/> remembered (it left the list).</summary>
	public static void ForgetStory(
		IDictionary<string, List<(DateTimeOffset Time, string Provider)>> stories,
		NewsHeadlineDto headline
	)
	{
		var key = GetStoryKey(headline);
		if (!stories.TryGetValue(key, out var seen))
			return;
		seen.RemoveAll(x =>
			x.Time == headline.Time && x.Provider == $"{headline.SourceId}|{headline.ProviderCode}"
		);
		if (seen.Count == 0)
			stories.Remove(key);
	}

	/// <summary>
	/// The newest-first merge of <see cref="Merge"/>, also dropping a later copy of the same story
	/// from another provider (<see cref="IsRepeatedStory"/>; the newer copy goes, the first
	/// published stays).
	/// </summary>
	public static List<NewsHeadlineDto> MergeStories(
		IEnumerable<NewsHeadlineDto> headlines,
		int max,
		TimeSpan window
	)
	{
		var keys = new HashSet<string>();
		var stories = new Dictionary<string, List<(DateTimeOffset, string)>>();
		return headlines
			.Where(x => keys.Add(GetKey(x)))
			.OrderBy(x => x.Time)
			.Where(x => !IsRepeatedStory(stories, x, window))
			.OrderByDescending(x => x.Time)
			.Take(max)
			.ToList();
	}

	/// <summary>Alert keywords as typed in Settings: comma- or line-separated words and phrases, trimmed.</summary>
	public static IReadOnlyList<string> ParseKeywords(string? keywords) =>
		string.IsNullOrWhiteSpace(keywords)
			? []
			:
			[
				.. keywords
					.Split([',', ';', '\n'], StringSplitOptions.TrimEntries)
					.Where(x => x.Length > 0)
					.Distinct(StringComparer.OrdinalIgnoreCase),
			];

	/// <summary>
	/// A "match": a keyword in the headline or its summary (whole words, any case), or a
	/// headline about one of <paramref name="symbols"/>.
	/// </summary>
	public static bool MatchesAlert(
		NewsHeadlineDto headline,
		IReadOnlyList<string> keywords,
		IReadOnlySet<string> symbols
	)
	{
		if (
			symbols.Count > 0
			&& (
				symbols.Contains(NewsFeedHelpers.ToFeedSymbol(headline.Symbol))
				|| headline.Symbols.Any(x => symbols.Contains(NewsFeedHelpers.ToFeedSymbol(x)))
			)
		)
			return true;
		foreach (var keyword in keywords)
		{
			var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(keyword)}(?![\p{{L}}\p{{N}}])";
			if (
				Regex.IsMatch(headline.Headline, pattern, RegexOptions.IgnoreCase)
				|| (
					headline.Summary != null
					&& Regex.IsMatch(headline.Summary, pattern, RegexOptions.IgnoreCase)
				)
			)
				return true;
		}
		return false;
	}

	/// <summary>Archive search: every word of the query in the headline, summary, provider or symbols.</summary>
	public static bool MatchesSearch(NewsHeadlineDto headline, string? query) =>
		SettingsSearchHelpers.Matches(
			query,
			headline.Headline,
			headline.Summary,
			headline.ProviderCode,
			string.Join(' ', headline.Symbols)
		);

	/// <summary>
	/// Whether a widget's saved selection selects the provider: no saved selection, or none for
	/// its source (saved before the source existed), means its default; otherwise whether its key
	/// is saved.
	/// </summary>
	public static bool IsProviderSelected(IReadOnlySet<string>? saved, NewsProviderDto provider)
	{
		if (saved == null)
			return provider.SelectedByDefault;
		var marker = NewsConstants.GetSourceMarker(provider.SourceId);
		return saved.Any(x => x.StartsWith(marker, StringComparison.Ordinal))
			? saved.Contains(GetProviderKey(provider))
			: provider.SelectedByDefault;
	}

	/// <summary>
	/// The selection to save: the selected keys of the providers shown, a marker per shown
	/// source (so "none of this source" is kept), and what was saved for sources not shown now
	/// (IB's while disconnected).
	/// </summary>
	public static List<string> MergeProviderSelection(
		IReadOnlyList<(NewsProviderDto Provider, bool Selected)> shown,
		IEnumerable<string>? saved
	)
	{
		var sources = shown.Select(x => x.Provider.SourceId).Distinct().ToList();
		var markers = sources.Select(NewsConstants.GetSourceMarker).ToList();
		var kept = (saved ?? []).Where(x =>
			!markers.Any(m => x.StartsWith(m, StringComparison.Ordinal))
		);
		return
		[
			.. kept,
			.. markers,
			.. shown.Where(x => x.Selected).Select(x => GetProviderKey(x.Provider)),
		];
	}

	/// <summary>Local filter: every word in the headline, provider or symbol, any case.</summary>
	public static bool Matches(NewsHeadlineDto headline, string? filter) =>
		SettingsSearchHelpers.Matches(
			filter,
			headline.Headline,
			headline.ProviderCode,
			headline.Symbol
		);

	/// <summary>
	/// The broad tape to subscribe to among a provider's: the one covering all sub-tapes
	/// (symbol ending in <c>_ALL</c>, e.g. BRFG:BRFG_ALL), else the first.
	/// </summary>
	public static ContractDto? PickBroadTape(IReadOnlyList<ContractDto> tapes) =>
		tapes.FirstOrDefault(x =>
			x.Symbol.EndsWith(NewsConstants.AllTapeSuffix, StringComparison.OrdinalIgnoreCase)
		) ?? tapes.FirstOrDefault();

	/// <summary>
	/// The tape another provider's headlines come on (<see cref="NewsConstants.SharedBroadTapes"/>,
	/// e.g. DJ-RT → DJ:N_DJN), or null when the provider isn't one of those.
	/// </summary>
	public static ContractDto? GetSharedBroadTape(string providerCode) =>
		NewsConstants.SharedBroadTapes.TryGetValue(providerCode, out var tape)
			? new ContractDto
			{
				Symbol = tape.Symbol,
				SecurityType = NewsConstants.NewsSecurityType,
				Exchange = tape.Exchange,
			}
			: null;

	/// <summary>
	/// A provider's broad tape: its shared one (<see cref="GetSharedBroadTape"/>, never looked
	/// up), else from <paramref name="cache"/>, or looked up with <paramref name="fetchTapes"/>
	/// and remembered (also "none"). A failed lookup isn't cached.
	/// </summary>
	public static async Task<ContractDto?> ResolveBroadTapeAsync(
		IDictionary<string, ContractDto?> cache,
		string providerCode,
		Func<string, CancellationToken, Task<IReadOnlyList<ContractDto>>> fetchTapes,
		CancellationToken cancellationToken
	)
	{
		if (GetSharedBroadTape(providerCode) is { } shared)
			return shared;
		if (cache.TryGetValue(providerCode, out var cached))
			return cached;
		var tape = PickBroadTape(await fetchTapes(providerCode, cancellationToken));
		cache[providerCode] = tape;
		return tape;
	}

	/// <summary>
	/// The providers' broad tapes, each tape once (in first-seen order), with the provider codes
	/// whose headlines to keep from it: null for a provider's own tape (all of it), the selected
	/// codes for a shared one (DJ:N_DJN carries every DJ-* provider, only the chosen are kept).
	/// </summary>
	public static List<(ContractDto Tape, IReadOnlySet<string>? ProviderCodes)> GroupBroadTapes(
		IEnumerable<(string ProviderCode, ContractDto Tape)> tapes
	)
	{
		var groups = new List<(ContractDto Tape, HashSet<string> Codes, bool Shared)>();
		foreach (var (code, tape) in tapes)
		{
			var index = groups.FindIndex(x =>
				x.Tape.ConId == tape.ConId
				&& x.Tape.Symbol == tape.Symbol
				&& x.Tape.Exchange == tape.Exchange
			);
			var shared = NewsConstants.SharedBroadTapes.ContainsKey(code);
			if (index < 0)
				groups.Add((tape, [code], shared));
			else
			{
				groups[index].Codes.Add(code);
				groups[index] = groups[index] with { Shared = groups[index].Shared || shared };
			}
		}
		return [.. groups.Select(x => (x.Tape, x.Shared ? (IReadOnlySet<string>?)x.Codes : null))];
	}

	/// <summary>IB sends text and HTML with the same type; HTML has tags.</summary>
	public static bool IsHtml(string body) => HtmlTagRegex().IsMatch(body);

	public static NewsArticleKind GetTextKind(string body) =>
		IsHtml(body) ? NewsArticleKind.Html : NewsArticleKind.Text;

	/// <summary>
	/// An article as plain text for the reading pane: HTML without tags (block tags become line
	/// breaks, <c>&lt;pre&gt;</c> keeps its layout), entities decoded (<c>&amp;#10;</c> included),
	/// at most one empty line in a row. Binary articles have no text: empty.
	/// </summary>
	public static string ToPlainText(NewsArticleDto article) =>
		article.Kind switch
		{
			NewsArticleKind.Html => HtmlToPlainText(article.Body),
			NewsArticleKind.Text => Tidy(WebUtility.HtmlDecode(article.Body)),
			_ => string.Empty,
		};

	public static string HtmlToPlainText(string html)
	{
		var body = ScriptRegex().Replace(html, string.Empty);
		var text = new StringBuilder();
		var position = 0;
		foreach (Match pre in PreRegex().Matches(body))
		{
			text.Append(FlowToText(body[position..pre.Index]));
			text.Append('\n')
				.Append(
					WebUtility.HtmlDecode(TagRegex().Replace(pre.Groups[1].Value, string.Empty))
				)
				.Append('\n');
			position = pre.Index + pre.Length;
		}
		text.Append(FlowToText(body[position..]));
		return Tidy(text.ToString());
	}

	/// <summary>Bytes of a binary (base64) article.</summary>
	public static byte[] DecodeBinary(NewsArticleDto article) =>
		Convert.FromBase64String(WhitespaceRegex().Replace(article.Body, string.Empty));

	/// <summary>A file name for a binary article, its extension from the content (PDF …).</summary>
	public static string GetDocumentFileName(NewsHeadlineDto headline, byte[] content)
	{
		var extension = content switch
		{
			[0x25, 0x50, 0x44, 0x46, ..] => ".pdf", // %PDF
			[0x50, 0x4B, ..] => ".zip",
			_ => ".bin",
		};
		var name = UnsafeFileCharsRegex()
			.Replace($"news-{headline.ProviderCode}-{headline.ArticleId}", "_");
		return name + extension;
	}

	private static double? ParseNumber(string value) =>
		double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
			? number
			: null;

	// Flowing HTML: source line breaks are just spaces; block tags break lines
	private static string FlowToText(string html)
	{
		var text = WhitespaceRegex().Replace(html, " ");
		text = BreakRegex().Replace(text, "\n");
		text = ListItemRegex().Replace(text, "\n• ");
		text = BlockTagRegex().Replace(text, "\n");
		text = TagRegex().Replace(text, string.Empty);
		return WebUtility.HtmlDecode(text);
	}

	private static string Tidy(string text)
	{
		var lines = text.Replace("\r\n", "\n")
			.Replace('\r', '\n')
			.Replace(' ', ' ')
			.Split('\n')
			.Select(x => x.TrimEnd());
		var result = BlankLinesRegex().Replace(string.Join('\n', lines), "\n\n");
		return result.Trim('\n', ' ');
	}

	[GeneratedRegex(
		@"<\s*/?\s*(html|body|head|p|div|pre|br|span|a|b|i|em|strong|table|tr|td|ul|ol|li|h[1-6])\b",
		RegexOptions.IgnoreCase
	)]
	private static partial Regex HtmlTagRegex();

	[GeneratedRegex(
		@"<\s*(script|style|head)\b[^>]*>.*?<\s*/\s*\1\s*>",
		RegexOptions.IgnoreCase | RegexOptions.Singleline
	)]
	private static partial Regex ScriptRegex();

	[GeneratedRegex(
		@"<\s*pre\b[^>]*>(.*?)<\s*/\s*pre\s*>",
		RegexOptions.IgnoreCase | RegexOptions.Singleline
	)]
	private static partial Regex PreRegex();

	[GeneratedRegex(@"<\s*br\s*/?\s*>", RegexOptions.IgnoreCase)]
	private static partial Regex BreakRegex();

	[GeneratedRegex(@"<\s*li\b[^>]*>", RegexOptions.IgnoreCase)]
	private static partial Regex ListItemRegex();

	[GeneratedRegex(
		@"<\s*/?\s*(p|div|tr|table|ul|ol|li|h[1-6]|blockquote|section|article|header|footer)\b[^>]*>",
		RegexOptions.IgnoreCase
	)]
	private static partial Regex BlockTagRegex();

	[GeneratedRegex(@"<[^>]*>")]
	private static partial Regex TagRegex();

	[GeneratedRegex(@"\s+")]
	private static partial Regex WhitespaceRegex();

	[GeneratedRegex(@"\n{3,}")]
	private static partial Regex BlankLinesRegex();

	[GeneratedRegex(@"[^A-Za-z0-9._-]")]
	private static partial Regex UnsafeFileCharsRegex();

	[GeneratedRegex(@"[^\p{L}\p{N}]+")]
	private static partial Regex NonWordRegex();
}
