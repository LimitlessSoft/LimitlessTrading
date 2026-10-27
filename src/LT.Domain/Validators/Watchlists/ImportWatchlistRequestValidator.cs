using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Watchlists;

namespace LT.Domain.Validators.Watchlists;

/// <summary>
/// The file must be ours and hold between one and the most items a watchlist can, each with a
/// contract id or a symbol to look it up by; its columns valid, distinct names, and their values not too long. The first
/// problem is reported, so the user can fix the file.
/// </summary>
public class ImportWatchlistRequestValidator : LSCoreValidatorBase<ImportWatchlistRequest>
{
	public ImportWatchlistRequestValidator()
	{
		RuleFor(x => x)
			.Custom(
				(request, context) =>
				{
					if (GetError(request) is { } error)
						context.AddFailure(nameof(request.Items), error);
				}
			);
	}

	private static string? GetError(ImportWatchlistRequest request)
	{
		if (request.Format != WatchlistConstants.FileFormat)
			return WatchlistValidationCodes.WVC_015.GetValidationMessage();
		if (request.Items.Count == 0)
			return WatchlistValidationCodes.WVC_016.GetValidationMessage();
		if (request.Items.Count > MarketDataConstants.WatchlistMaxItems)
			return WatchlistValidationCodes.WVC_002.GetValidationMessage(
				MarketDataConstants.WatchlistMaxItems
			);
		var missing = request
			.Items.Select((item, index) => (item, index))
			.FirstOrDefault(x =>
				x.item.Contract is not { ConId: > 0 }
				&& string.IsNullOrWhiteSpace(x.item.Contract?.Symbol)
			);
		if (missing.item != null)
			return WatchlistValidationCodes.WVC_021.GetValidationMessage(
				WatchlistFileHelpers.DescribeItem(missing.item.Contract, missing.index)
			);
		// One without a conId is looked up by its symbol, which must look like one
		var badSymbol = request
			.Items.Select(x => x.Contract!)
			.FirstOrDefault(x =>
				x.ConId <= 0
				&& !WatchlistFileHelpers.IsValidSymbol((x.Symbol ?? string.Empty).Trim())
			);
		if (badSymbol != null)
			return WatchlistValidationCodes.WVC_018.GetValidationMessage(
				(badSymbol.Symbol ?? string.Empty).Trim()
			);
		if (request.Columns.Count > WatchlistConstants.MaxCustomColumns)
			return WatchlistValidationCodes.WVC_012.GetValidationMessage(
				WatchlistConstants.MaxCustomColumns
			);
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var column in request.Columns)
		{
			if (WatchlistHelpers.GetColumnNameError(column) is { } nameError)
				return nameError;
			if (!names.Add(column.Trim()))
				return WatchlistValidationCodes.WVC_011.GetValidationMessage(column.Trim());
		}
		return request
			.Items.SelectMany(x => x.Values.Values)
			.Select(WatchlistHelpers.GetCellValueError)
			.FirstOrDefault(x => x != null);
	}
}
