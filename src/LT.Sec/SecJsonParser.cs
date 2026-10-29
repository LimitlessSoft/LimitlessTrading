using System.Globalization;
using System.Text.Json;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Fundamentals;

namespace LT.Sec;

/// <summary>Reads the SEC's EDGAR JSON (company tickers, XBRL concepts and company facts).</summary>
public static class SecJsonParser
{
	/// <summary>
	/// company_tickers.json: <c>{"0":{"cik_str":320193,"ticker":"AAPL","title":"Apple Inc."},…}</c>.
	/// A ticker listed twice keeps its first CIK.
	/// </summary>
	public static IReadOnlyDictionary<string, int> ParseCompanyTickers(string json)
	{
		using var document = JsonDocument.Parse(json);
		var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (var company in document.RootElement.EnumerateObject())
		{
			if (
				company.Value.ValueKind != JsonValueKind.Object
				|| !company.Value.TryGetProperty("ticker", out var ticker)
				|| ticker.ValueKind != JsonValueKind.String
				|| !company.Value.TryGetProperty("cik_str", out var cik)
				|| !cik.TryGetInt32(out var cikValue)
			)
				continue;
			var symbol = ticker.GetString();
			if (!string.IsNullOrWhiteSpace(symbol))
				map.TryAdd(symbol.Trim().ToUpperInvariant(), cikValue);
		}
		return map;
	}

	/// <summary>A companyconcept answer: its <c>units.shares</c> values.</summary>
	public static IReadOnlyList<SecFactDto> ParseConcept(string json)
	{
		using var document = JsonDocument.Parse(json);
		var tag =
			document.RootElement.TryGetProperty("tag", out var name)
			&& name.ValueKind == JsonValueKind.String
				? name.GetString() ?? string.Empty
				: string.Empty;
		return ReadShareFacts(document.RootElement, tag).ToList();
	}

	/// <summary>
	/// A companyfacts answer: the shares values of dei:EntityCommonStockSharesOutstanding,
	/// us-gaap:CommonStockSharesOutstanding and the us-gaap weighted averages (basic, diluted)
	/// together.
	/// </summary>
	public static IReadOnlyList<SecFactDto> ParseCompanyShareFacts(string json)
	{
		using var document = JsonDocument.Parse(json);
		if (!document.RootElement.TryGetProperty("facts", out var facts))
			return [];
		return ReadConcept(facts, SecConstants.DeiTaxonomy, SecConstants.SharesOutstandingConcept)
			.Concat(
				ReadConcept(
					facts,
					SecConstants.UsGaapTaxonomy,
					SecConstants.CommonStockSharesOutstandingConcept
				)
			)
			.Concat(
				ReadConcept(
					facts,
					SecConstants.UsGaapTaxonomy,
					SecConstants.WeightedAverageBasicConcept
				)
			)
			.Concat(
				ReadConcept(
					facts,
					SecConstants.UsGaapTaxonomy,
					SecConstants.WeightedAverageDilutedConcept
				)
			)
			.ToList();
	}

	/// <summary>
	/// A companyfacts answer: the company's name and every value of the us-gaap
	/// <paramref name="concepts"/>, in whichever unit each is reported (USD, USD/shares, shares).
	/// </summary>
	public static SecCompanyFactsDto ParseCompanyFinancialFacts(
		string json,
		IReadOnlyCollection<string> concepts
	)
	{
		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;
		var name =
			root.TryGetProperty("entityName", out var entity)
			&& entity.ValueKind == JsonValueKind.String
				? entity.GetString()
				: null;
		var facts = new List<SecFactDto>();
		if (
			root.TryGetProperty("facts", out var all)
			&& all.TryGetProperty(SecConstants.UsGaapTaxonomy, out var usGaap)
			&& usGaap.ValueKind == JsonValueKind.Object
		)
			foreach (var concept in usGaap.EnumerateObject())
				if (
					concepts.Contains(concept.Name)
					&& concept.Value.TryGetProperty("units", out var units)
					&& units.ValueKind == JsonValueKind.Object
				)
					foreach (var unit in units.EnumerateObject())
						facts.AddRange(ReadFacts(concept.Value, concept.Name, unit.Name));
		return new SecCompanyFactsDto { EntityName = name, Facts = facts };
	}

	/// <summary>
	/// A submissions answer: the newest of its recent filings in <paramref name="forms"/> that is
	/// inline XBRL (EDGAR extracts an instance document only from those); null when there is none.
	/// </summary>
	public static SecFilingRef? ParseLatestInlineFiling(string json, IReadOnlyList<string> forms)
	{
		using var document = JsonDocument.Parse(json);
		if (
			!document.RootElement.TryGetProperty("filings", out var filings)
			|| !filings.TryGetProperty("recent", out var recent)
			|| !TryGetArray(recent, "form", out var formList)
			|| !TryGetArray(recent, "accessionNumber", out var accessions)
			|| !TryGetArray(recent, "filingDate", out var dates)
			|| !TryGetArray(recent, "primaryDocument", out var documents)
			|| !TryGetArray(recent, "isInlineXBRL", out var inline)
		)
			return null;

		SecFilingRef? latest = null;
		var count = new[] { formList, accessions, dates, documents, inline }.Min(x =>
			x.GetArrayLength()
		);
		for (var i = 0; i < count; i++)
		{
			var form = formList[i].GetString() ?? string.Empty;
			if (
				!forms.Contains(form)
				|| !inline[i].TryGetInt32(out var isInline)
				|| isInline != 1
				|| !DateOnly.TryParseExact(
					dates[i].GetString(),
					"yyyy-MM-dd",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None,
					out var filed
				)
				|| documents[i].GetString() is not { Length: > 0 } primary
				|| !primary.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)
				|| accessions[i].GetString() is not { Length: > 0 } accession
			)
				continue;
			if (latest == null || filed > latest.Filed)
				latest = new SecFilingRef(accession, primary, form, filed);
		}
		return latest;
	}

	private static bool TryGetArray(JsonElement element, string name, out JsonElement array) =>
		element.TryGetProperty(name, out array) && array.ValueKind == JsonValueKind.Array;

	private static IEnumerable<SecFactDto> ReadConcept(
		JsonElement facts,
		string taxonomy,
		string concept
	) =>
		facts.TryGetProperty(taxonomy, out var group)
		&& group.TryGetProperty(concept, out var element)
			? ReadShareFacts(element, concept)
			: [];

	private static IEnumerable<SecFactDto> ReadShareFacts(JsonElement concept, string name) =>
		ReadFacts(concept, name, SecConstants.SharesUnit);

	// { "units": { "<unit>": [ { "start"?, "end", "val", "form", "filed", … } ] } }
	private static IEnumerable<SecFactDto> ReadFacts(JsonElement concept, string name, string unit)
	{
		if (
			!concept.TryGetProperty("units", out var units)
			|| !units.TryGetProperty(unit, out var values)
			|| values.ValueKind != JsonValueKind.Array
		)
			yield break;

		foreach (var value in values.EnumerateArray())
		{
			if (
				TryGetDate(value, "end", out var end)
				&& TryGetDate(value, "filed", out var filed)
				&& value.TryGetProperty("val", out var val)
				&& val.TryGetDecimal(out var number)
			)
				yield return new SecFactDto
				{
					Concept = name,
					Start = TryGetDate(value, "start", out var start) ? start : null,
					End = end,
					Filed = filed,
					Value = number,
					Form = value.TryGetProperty("form", out var form)
						? form.GetString() ?? string.Empty
						: string.Empty,
				};
		}
	}

	private static bool TryGetDate(JsonElement element, string name, out DateOnly date)
	{
		date = default;
		return element.TryGetProperty(name, out var value)
			&& value.ValueKind == JsonValueKind.String
			&& DateOnly.TryParseExact(
				value.GetString(),
				"yyyy-MM-dd",
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out date
			);
	}
}
