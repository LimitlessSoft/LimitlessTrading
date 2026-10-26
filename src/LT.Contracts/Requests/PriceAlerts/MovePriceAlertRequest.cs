namespace LT.Contracts.Requests.PriceAlerts;

/// <summary>A new level for an alert; a triggered alert is armed again.</summary>
public class MovePriceAlertRequest
{
	public string Id { get; set; } = string.Empty;
	public double Price { get; set; }

	/// <summary>The contract's price now; the direction is told again from it.</summary>
	public double? CurrentPrice { get; set; }
}
