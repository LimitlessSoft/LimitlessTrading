using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LT.Contracts.Dtos.Calendar;

namespace LT.Sec;

/// <summary>
/// Reads EDGAR's latest filings Atom feed (<c>browse-edgar?action=getcurrent&amp;output=atom</c>),
/// as recorded on 2026-10-29. An entry:
/// <c>&lt;title&gt;8-K - AMAZON COM INC (0001018724) (Filer)&lt;/title&gt;</c>, the index page in
/// <c>link/@href</c>, an HTML summary listing <c>Item 2.02: …</c> lines, the acceptance time in
/// <c>updated</c> and the accession number in <c>id</c>. Entries it can't read are skipped; a
/// document that isn't the feed throws.
/// </summary>
public static partial class SecAtomParser
{
	private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

	public static IReadOnlyList<SecCurrentFilingDto> ParseCurrentFilings(string xml)
	{
		XDocument document;
		try
		{
			document = XDocument.Parse(xml);
		}
		catch (System.Xml.XmlException ex)
		{
			throw new FormatException("EDGAR's latest filings feed could not be read.", ex);
		}
		if (document.Root?.Name != Atom + "feed")
			throw new FormatException(
				"EDGAR answered something other than its latest filings feed."
			);

		var filings = new List<SecCurrentFilingDto>();
		foreach (var entry in document.Root.Elements(Atom + "entry"))
		{
			var title = TitlePattern()
				.Match((string?)entry.Element(Atom + "title") ?? string.Empty);
			var id = AccessionPattern().Match((string?)entry.Element(Atom + "id") ?? string.Empty);
			if (
				!title.Success
				|| !id.Success
				|| !DateTimeOffset.TryParse(
					(string?)entry.Element(Atom + "updated"),
					CultureInfo.InvariantCulture,
					DateTimeStyles.None,
					out var accepted
				)
			)
				continue;
			var summary = WebUtility.HtmlDecode(
				(string?)entry.Element(Atom + "summary") ?? string.Empty
			);
			filings.Add(
				new SecCurrentFilingDto
				{
					Cik = int.Parse(title.Groups["cik"].Value, CultureInfo.InvariantCulture),
					Company = WebUtility.HtmlDecode(title.Groups["company"].Value.Trim()),
					Form = title.Groups["form"].Value.Trim(),
					AccessionNumber = id.Groups["number"].Value,
					AcceptedAt = accepted,
					Items = [.. ItemPattern().Matches(summary).Select(x => x.Groups["item"].Value)],
					Url = (string?)entry.Element(Atom + "link")?.Attribute("href") ?? string.Empty,
				}
			);
		}
		return filings;
	}

	// "8-K - AMAZON COM INC (0001018724) (Filer)"
	[GeneratedRegex(
		@"^\s*(?<form>\S+)\s+-\s+(?<company>.*)\((?<cik>\d{1,10})\)\s*\((?:Filer|Subject|Filed by)\)\s*$"
	)]
	private static partial Regex TitlePattern();

	[GeneratedRegex(@"accession-number=(?<number>[\d-]+)")]
	private static partial Regex AccessionPattern();

	[GeneratedRegex(@"Item\s+(?<item>\d+\.\d+)\s*:")]
	private static partial Regex ItemPattern();
}
