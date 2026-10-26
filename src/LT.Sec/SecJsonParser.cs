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
		return ReadShareFacts(document.RootElement).ToList();
	}

	/// <summary>
	/// A companyfacts answer: the shares values of dei:EntityCommonStockSharesOutstanding and
	/// us-gaap:CommonStockSharesOutstanding together.
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
			.ToList();
	}

	private static IEnumerable<SecFactDto> ReadConcept(
		JsonElement facts,
		string taxonomy,
		string concept
	) =>
		facts.TryGetProperty(taxonomy, out var group)
		&& group.TryGetProperty(concept, out var element)
			? ReadShareFacts(element)
			: [];

	// { "units": { "shares": [ { "end", "val", "form", "filed", … } ] } }
	private static IEnumerable<SecFactDto> ReadShareFacts(JsonElement concept)
	{
		if (
			!concept.TryGetProperty("units", out var units)
			|| !units.TryGetProperty(SecConstants.SharesUnit, out var values)
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
