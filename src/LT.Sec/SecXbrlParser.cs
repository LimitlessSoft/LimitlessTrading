using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Fundamentals;

namespace LT.Sec;

/// <summary>Reads an XBRL instance document (what EDGAR extracts from an inline-XBRL filing).</summary>
public static class SecXbrlParser
{
	private static readonly XNamespace Xbrli = SecConstants.XbrlInstanceNamespace;
	private static readonly XNamespace Xbrldi = SecConstants.XbrlDimensionsNamespace;

	/// <summary>
	/// The cover page's dei:EntityCommonStockSharesOutstanding values: undivided, or one per
	/// class-of-stock member (<see cref="SecFactDto.ShareClass"/>). Counts on any other dimension
	/// (one co-registrant of several) are skipped. Form and filing date are the filing's.
	/// </summary>
	public static IReadOnlyList<SecFactDto> ParseCoverPageShares(
		string xml,
		string form,
		DateOnly filed
	)
	{
		var contexts = new Dictionary<string, (DateOnly End, string? ShareClass)>(
			StringComparer.Ordinal
		);
		var values = new List<(string Context, decimal Value)>();
		using var reader = XmlReader.Create(
			new StringReader(xml),
			new XmlReaderSettings
			{
				DtdProcessing = DtdProcessing.Prohibit,
				IgnoreComments = true,
				IgnoreWhitespace = true,
			}
		);
		reader.MoveToContent();
		while (!reader.EOF)
		{
			if (reader.NodeType != XmlNodeType.Element)
			{
				reader.Read();
				continue;
			}
			if (reader.LocalName == "context" && reader.NamespaceURI == Xbrli.NamespaceName)
			{
				if (XNode.ReadFrom(reader) is XElement context && ReadContext(context) is { } read)
					contexts[read.Id] = (read.End, read.ShareClass);
				continue;
			}
			if (
				reader.LocalName == SecConstants.SharesOutstandingConcept
				&& reader.NamespaceURI.StartsWith(
					SecConstants.DeiNamespacePrefix,
					StringComparison.Ordinal
				)
			)
			{
				var contextRef = reader.GetAttribute("contextRef");
				var text = reader.ReadElementContentAsString();
				if (
					contextRef != null
					&& decimal.TryParse(
						text,
						NumberStyles.Number,
						CultureInfo.InvariantCulture,
						out var value
					)
				)
					values.Add((contextRef, value));
				continue;
			}
			reader.Read();
		}

		return values
			.Where(x => contexts.ContainsKey(x.Context))
			.Select(x => new SecFactDto
			{
				Concept = SecConstants.SharesOutstandingConcept,
				End = contexts[x.Context].End,
				Filed = filed,
				Value = x.Value,
				Form = form,
				ShareClass = contexts[x.Context].ShareClass,
			})
			.ToList();
	}

	// Its date and class member; null when it has another dimension (or no readable date)
	private static (string Id, DateOnly End, string? ShareClass)? ReadContext(XElement context)
	{
		var id = (string?)context.Attribute("id");
		var period = context.Element(Xbrli + "period");
		var date =
			(string?)period?.Element(Xbrli + "instant")
			?? (string?)period?.Element(Xbrli + "endDate");
		if (
			id == null
			|| !DateOnly.TryParseExact(
				date?.Trim(),
				"yyyy-MM-dd",
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out var end
			)
		)
			return null;

		var dimensions = context.Descendants().Where(x => x.Name.Namespace == Xbrldi).ToList();
		if (dimensions.Count == 0)
			return (id, end, null);
		if (
			dimensions.Count == 1
			&& dimensions[0].Name == Xbrldi + "explicitMember"
			&& LocalName((string?)dimensions[0].Attribute("dimension"))
				== SecConstants.ClassOfStockAxis
			&& LocalName(dimensions[0].Value) is { Length: > 0 } member
		)
			return (id, end, member);
		return null;
	}

	// "us-gaap:CommonClassAMember" → "CommonClassAMember"
	private static string? LocalName(string? qualified) =>
		qualified?.Trim() is { } name ? name[(name.IndexOf(':') + 1)..] : null;
}
