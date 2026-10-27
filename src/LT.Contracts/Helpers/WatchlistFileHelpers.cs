using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LT.Contracts.Constants;
using LT.Contracts.DtoMappings.Contracts;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Watchlists;
using LT.Contracts.Entities;
using LT.Contracts.Requests.Watchlists;

namespace LT.Contracts.Helpers;

/// <summary>
/// Watchlist files: the JSON export (items in saved order, pins, custom columns and their values)
/// and plain lists of symbols (one per line or comma-separated) accepted on import.
/// </summary>
public static class WatchlistFileHelpers
{
	// camelCase on write, non-ASCII and quotes left readable (a file, never HTML); reading takes
	// any case, comments and trailing commas (hand-edited files) and ignores fields it doesn't know
	private static readonly JsonSerializerOptions FileJsonOptions = new()
	{
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	// Separators of a plain symbol list besides line breaks (CSV, semicolon CSV, TSV)
	private static readonly char[] SymbolSeparators = [',', ';', '\t'];

	// A first entry naming the column of a CSV export, not a symbol
	private static readonly HashSet<string> SymbolHeaders = new(StringComparer.OrdinalIgnoreCase)
	{
		"symbol",
		"symbols",
		"ticker",
		"tickers",
	};

	/// <summary>The watchlist as an export file: items in saved order, with pins and custom values.</summary>
	public static string Serialize(WatchlistEntity watchlist)
	{
		var mapper = new ContractDtoMapper();
		var items = watchlist
			.Items.Select(item =>
			{
				var contract = mapper.ToMapped(item);
				var pin = watchlist.Pinned.FindIndex(x => WatchlistHelpers.IsPinOf(x, contract));
				var values = watchlist
					.Columns.Select(column =>
						(
							column.Name,
							Value: column
								.Cells.FirstOrDefault(x => WatchlistHelpers.IsCellOf(x, contract))
								?.Value
						)
					)
					.Where(x => !string.IsNullOrEmpty(x.Value))
					.ToDictionary(x => x.Name, x => x.Value!);
				return new WatchlistFileItemEntity
				{
					Contract = item,
					Pin = pin >= 0 ? pin : null,
					Values = values.Count > 0 ? values : null,
				};
			})
			.ToList();
		return JsonSerializer.Serialize(
			new WatchlistFileEntity
			{
				Format = WatchlistConstants.FileFormat,
				Version = WatchlistConstants.FileVersion,
				Name = watchlist.Name,
				Columns = [.. watchlist.Columns.Select(x => x.Name)],
				Items = items,
			},
			FileJsonOptions
		);
	}

	/// <summary>The file's content; null when it isn't JSON of that shape.</summary>
	public static WatchlistFileEntity? Parse(string json)
	{
		try
		{
			return JsonSerializer.Deserialize<WatchlistFileEntity>(json, FileJsonOptions);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	/// <summary>Whether the content is text at all (no NUL or other control characters but line breaks and tabs).</summary>
	public static bool IsText(string content) =>
		!content.Any(x => char.IsControl(x) && x is not ('\r' or '\n' or '\t'));

	/// <summary>Symbols for a message: the first few, then "…".</summary>
	public static string JoinSymbols(IReadOnlyCollection<string> symbols, int max = 10) =>
		string.Join(", ", symbols.Take(max)) + (symbols.Count > max ? ", …" : string.Empty);

	/// <summary>Whether the content is meant as JSON (an export file) rather than a list of symbols.</summary>
	public static bool IsJson(string content) =>
		content.TrimStart().TrimStart('﻿').TrimStart().StartsWith('{');

	/// <summary>
	/// The file's content as an import request: items in file order, values by column name
	/// (case-insensitive). Null entries are dropped; an item without a contract is kept, for
	/// validation to name it.
	/// </summary>
	public static ImportWatchlistRequest ToRequest(WatchlistFileEntity file)
	{
		var mapper = new ContractDtoMapper();
		return new ImportWatchlistRequest
		{
			Format = file.Format,
			Name = file.Name,
			Columns = [.. (file.Columns ?? []).Select(x => x ?? string.Empty)],
			Items =
			[
				.. (file.Items ?? [])
					.Where(x => x != null)
					.Select(x => new WatchlistImportItemDto
					{
						Contract = x.Contract != null ? mapper.ToMapped(x.Contract) : null,
						Pin = x.Pin,
						Values = new Dictionary<string, string>(
							(x.Values ?? [])
								.Where(v => v.Key != null && v.Value != null)
								.DistinctBy(v => v.Key, StringComparer.OrdinalIgnoreCase),
							StringComparer.OrdinalIgnoreCase
						),
					}),
			],
		};
	}

	/// <summary>
	/// A checked import (<see cref="ToRequest"/>) as a new watchlist named <paramref name="name"/>:
	/// the same listing twice is kept once, pins in their order, values trimmed (blank ones left
	/// out).
	/// </summary>
	public static WatchlistEntity ToEntity(ImportWatchlistRequest request, string name)
	{
		var mapper = new ContractEntityMapper();
		var items = new List<WatchlistImportItemDto>();
		foreach (var item in request.Items)
			if (
				item.Contract != null
				&& !items.Any(x => ContractHelpers.IsSameListing(x.Contract!, item.Contract))
			)
				items.Add(item);

		return new WatchlistEntity
		{
			Name = name,
			Items = [.. items.Select(x => mapper.ToMapped(x.Contract!))],
			Pinned =
			[
				.. items
					.Where(x => x.Pin is not null)
					.OrderBy(x => x.Pin)
					.Select(x => WatchlistHelpers.CreatePin(x.Contract!)),
			],
			Columns =
			[
				.. request.Columns.Select(column => new WatchlistColumnEntity
				{
					Name = column.Trim(),
					Cells =
					[
						.. items
							.Select(x =>
								(
									x.Contract,
									Value: x.Values.TryGetValue(column.Trim(), out var value)
										? value.Trim()
										: string.Empty
								)
							)
							.Where(x => x.Value.Length > 0)
							.Select(x => WatchlistHelpers.CreateCell(x.Contract!, x.Value)),
					],
				}),
			],
		};
	}

	/// <summary>
	/// The symbols of a plain list: one per line or separated by commas, semicolons or tabs;
	/// quotes around them, blank entries, "#" comment lines and a "Symbol" / "Ticker" header are
	/// dropped. Upper-cased, each once, in file order.
	/// </summary>
	public static List<string> ParseSymbols(string content)
	{
		var symbols = new List<string>();
		var lines = content
			.TrimStart('﻿')
			.Split(
				['\r', '\n'],
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
			)
			.Where(x => !x.StartsWith('#'));
		foreach (var line in lines)
		foreach (var entry in line.Split(SymbolSeparators))
		{
			var symbol = entry.Trim().Trim('"', '\'').Trim().ToUpperInvariant();
			if (symbol.Length == 0 || symbols.Contains(symbol))
				continue;
			if (symbols.Count == 0 && SymbolHeaders.Contains(symbol))
				continue;
			symbols.Add(symbol);
		}
		return symbols;
	}

	/// <summary>
	/// Whether a list entry can be a ticker: letters, digits and <c>. - / &amp;</c> or inner
	/// spaces ("BRK B"), at most <see cref="MarketDataConstants.SymbolMaxLength"/> long.
	/// </summary>
	public static bool IsValidSymbol(string symbol) =>
		symbol.Length is > 0 and <= MarketDataConstants.SymbolMaxLength
		&& char.IsAsciiLetterOrDigit(symbol[0])
		&& symbol.All(x => char.IsAsciiLetterOrDigit(x) || x is '.' or '-' or '/' or '&' or ' ');

	/// <summary>A file name for exporting <paramref name="watchlistName"/>, without the characters file systems refuse.</summary>
	public static string GetFileName(string watchlistName)
	{
		var invalid = Path.GetInvalidFileNameChars()
			.Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|'])
			.ToHashSet();
		var name = new string([.. watchlistName.Trim().Select(x => invalid.Contains(x) ? '_' : x)]);
		return (name.Length > 0 ? name : MarketDataConstants.DefaultWatchlistName)
			+ WatchlistConstants.FileExtension;
	}

	/// <summary>
	/// The name an imported watchlist starts from: the file's own (an export), else the file name
	/// without its extension, else "Imported"; cut to the longest watchlist name.
	/// </summary>
	public static string GetImportName(string? fileWatchlistName, string? fileName)
	{
		var name =
			!string.IsNullOrWhiteSpace(fileWatchlistName) ? fileWatchlistName.Trim()
			: !string.IsNullOrWhiteSpace(fileName)
				? Path.GetFileNameWithoutExtension(fileName.Trim()).Trim()
			: string.Empty;
		if (name.Length == 0)
			name = WatchlistConstants.ImportedWatchlistName;
		return name.Length > MarketDataConstants.WatchlistNameMaxLength
			? name[..MarketDataConstants.WatchlistNameMaxLength].TrimEnd()
			: name;
	}

	/// <summary>An import item in messages: its symbol, else "Item N" (1-based).</summary>
	public static string DescribeItem(ContractDto? contract, int index) =>
		!string.IsNullOrWhiteSpace(contract?.Symbol) ? contract.Symbol : $"Item {index + 1}";
}
