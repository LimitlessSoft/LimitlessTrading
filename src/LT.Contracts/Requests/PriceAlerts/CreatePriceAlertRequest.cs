using LT.Contracts.Dtos.Contracts;

namespace LT.Contracts.Requests.PriceAlerts;

public class CreatePriceAlertRequest
{
	public ContractDto? Contract { get; set; }

	/// <summary>The level.</summary>
	public double Price { get; set; }

	/// <summary>
	/// The contract's price now (the chart's last price): a level above it alerts on the way up,
	/// one at or below it on the way down.
	/// </summary>
	public double? CurrentPrice { get; set; }

	public string? Note { get; set; }
}
