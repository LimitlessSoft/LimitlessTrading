using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.PriceAlerts;

/// <summary>A price alert: fires once when the contract's price crosses its level.</summary>
public class PriceAlertDto
{
	public string Id { get; set; } = string.Empty;
	public ContractDto Contract { get; set; } = new();
	public double Price { get; set; }
	public PriceAlertCondition Condition { get; set; }
	public string? Note { get; set; }
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>When it fired; null while it is active.</summary>
	public DateTimeOffset? TriggeredAt { get; set; }

	/// <summary>The price that fired it.</summary>
	public double? TriggerPrice { get; set; }

	public bool IsTriggered => TriggeredAt != null;
}
