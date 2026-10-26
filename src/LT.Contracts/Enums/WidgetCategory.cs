namespace LT.Contracts.Enums;

/// <summary>
/// Where a widget's content comes from: menu → Widgets lists each category as its own section.
/// Not persisted; add values at the end anyway.
/// </summary>
public enum WidgetCategory
{
	/// <summary>Backed by Interactive Brokers: needs a connection for live data.</summary>
	Ibkr = 0,

	/// <summary>Works on its own, without IB (notes, …).</summary>
	Tools = 1,
}
