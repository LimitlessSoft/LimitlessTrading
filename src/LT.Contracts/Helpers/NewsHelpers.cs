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
		$"{provider.SourceId}{NewsConstants.ProviderKeySeparator}{provider.Code}";

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
	/// A provider's broad tape from <paramref name="cache"/>, or looked up with
	/// <paramref name="fetchTapes"/> and remembered (also "none"). A failed lookup isn't cached.
	/// </summary>
	public static async Task<ContractDto?> ResolveBroadTapeAsync(
		IDictionary<string, ContractDto?> cache,
		string providerCode,
		Func<string, CancellationToken, Task<IReadOnlyList<ContractDto>>> fetchTapes,
		CancellationToken cancellationToken
	)
	{
		if (cache.TryGetValue(providerCode, out var cached))
			return cached;
		var tape = PickBroadTape(await fetchTapes(providerCode, cancellationToken));
		cache[providerCode] = tape;
		return tape;
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
}
