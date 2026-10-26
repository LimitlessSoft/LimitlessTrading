namespace LT.Contracts.Enums;

/// <summary>
/// Which way the price has to cross a price alert's level. Saved in price-alerts.json as a
/// number: add values at the end only.
/// </summary>
public enum PriceAlertCondition
{
	/// <summary>Fires once the price is at or above the level (it was below when set).</summary>
	CrossingUp,

	/// <summary>Fires once the price is at or below the level (it was above when set).</summary>
	CrossingDown,
}
