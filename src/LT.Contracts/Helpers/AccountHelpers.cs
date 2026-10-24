using System.Globalization;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Account;

namespace LT.Contracts.Helpers;

public static class AccountHelpers
{
	public static decimal? ParseValue(string? value) =>
		decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
			? result
			: null;

	/// <summary>Builds the summary of one account from reqAccountSummary rows.</summary>
	public static AccountSummaryDto BuildSummary(
		string account,
		IEnumerable<AccountValueDto> values,
		DateTimeOffset updatedAt
	)
	{
		var rows = values
			.Where(x => x.Account == account)
			.GroupBy(x => x.Tag)
			.ToDictionary(x => x.Key, x => x.First());

		decimal? Get(string tag) =>
			rows.TryGetValue(tag, out var row) ? ParseValue(row.Value) : null;

		return new AccountSummaryDto
		{
			Account = account,
			Currency = rows.TryGetValue(AccountConstants.NetLiquidation, out var netLiq)
				? netLiq.Currency
				: string.Empty,
			NetLiquidation = Get(AccountConstants.NetLiquidation),
			TotalCashValue = Get(AccountConstants.TotalCashValue),
			BuyingPower = Get(AccountConstants.BuyingPower),
			AvailableFunds = Get(AccountConstants.AvailableFunds),
			ExcessLiquidity = Get(AccountConstants.ExcessLiquidity),
			InitMarginReq = Get(AccountConstants.InitMarginReq),
			MaintMarginReq = Get(AccountConstants.MaintMarginReq),
			GrossPositionValue = Get(AccountConstants.GrossPositionValue),
			UpdatedAt = updatedAt,
		};
	}
}
