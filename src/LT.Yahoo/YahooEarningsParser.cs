using System.Net;
using System.Text.RegularExpressions;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;

namespace LT.Yahoo;

/// <summary>
/// Reads Yahoo Finance's earnings calendar page, as recorded on 2026-10-29: a server-rendered
/// <c>&lt;table&gt;</c> whose rows are <c>&lt;tr data-testid="data-table-v2-row"&gt;</c> and
/// cells <c>&lt;td data-testid-cell="ticker|epsestimate|epsactual|epssurprisepct|…"&gt;</c>
/// ("-" for none, the surprise signed: "+17.88"), and "1-100 of 298" under it. Cells are found
/// by their names, not their order. A page without a table is a day without earnings (or past
/// the last page); a table whose rows can't be read throws instead of becoming an empty list.
/// </summary>
public static partial class YahooEarningsParser
{
	public static YahooEarningsPageDto Parse(string html, DateOnly day)
	{
		var rows = new List<EarningsResultDto>();
		var seenRows = 0;
		foreach (Match row in RowPattern().Matches(html))
		{
			seenRows++;
			var cells = CellPattern()
				.Matches(row.Groups["body"].Value)
				.ToDictionary(
					x => x.Groups["name"].Value,
					x => Clean(x.Groups["body"].Value),
					StringComparer.OrdinalIgnoreCase
				);
			if (
				!cells.TryGetValue("ticker", out var symbol)
				|| symbol.Length == 0
				|| !cells.ContainsKey("epsactual")
			)
				continue;
			var actual = ParseNumber(cells["epsactual"]);
			rows.Add(
				new EarningsResultDto
				{
					Symbol = symbol.ToUpperInvariant(),
					Date = day,
					EpsActual = actual,
					EpsSurprisePercent = actual is null
						? null
						: ParseNumber(cells.GetValueOrDefault("epssurprisepct")),
					Source = YahooConstants.SourceName,
				}
			);
		}
		if (rows.Count == 0 && (seenRows > 0 || html.Contains("<table", StringComparison.Ordinal)))
			throw new YahooServiceException(
				CalendarValidationCodes.CLVC_012.GetValidationMessage(
					CalendarHelpers.GetSnippet(seenRows > 0 ? RowPattern().Match(html).Value : html)
				)
			);

		var range = RangePattern().Match(html);
		return new YahooEarningsPageDto
		{
			Rows = rows,
			First = range.Success ? int.Parse(range.Groups["first"].Value) : 0,
			Total = range.Success ? int.Parse(range.Groups["total"].Value) : 0,
		};
	}

	// "7.48", "-1.32", "+17.88", "1,234.5"; "-" and blank are none
	private static decimal? ParseNumber(string? text) =>
		CalendarHelpers.ParseFigure(text?.Trim().TrimStart('+'));

	private static string Clean(string html) =>
		WebUtility.HtmlDecode(TagPattern().Replace(html, " ")).Replace(' ', ' ').Trim();

	[GeneratedRegex(
		"""<tr\b[^>]*data-testid="data-table-v2-row"[^>]*>(?<body>.*?)</tr>""",
		RegexOptions.Singleline
	)]
	private static partial Regex RowPattern();

	[GeneratedRegex(
		"""<td\b[^>]*data-testid-cell="(?<name>[^"]+)"[^>]*>(?<body>.*?)</td>""",
		RegexOptions.Singleline
	)]
	private static partial Regex CellPattern();

	[GeneratedRegex("<[^>]+>")]
	private static partial Regex TagPattern();

	[GeneratedRegex(
		"""class="total[^"]*">\s*(?<first>\d+)\s*-\s*(?<last>\d+)\s+of\s+(?<total>\d+)"""
	)]
	private static partial Regex RangePattern();
}
