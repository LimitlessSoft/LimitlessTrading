using LT.Contracts.Enums;

namespace LT.Contracts.Entities;

/// <summary>One price alert as saved in price-alerts.json.</summary>
public class PriceAlertEntity
{
	public string Id { get; set; } = string.Empty;
	public ContractEntity Contract { get; set; } = new();
	public double Price { get; set; }
	public PriceAlertCondition Condition { get; set; }
	public string? Note { get; set; }
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>When it fired; null while it is active. Alerts fire once.</summary>
	public DateTimeOffset? TriggeredAt { get; set; }

	/// <summary>The price that fired it.</summary>
	public double? TriggerPrice { get; set; }
}
