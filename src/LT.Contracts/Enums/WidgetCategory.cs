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

	/// <summary>
	/// Both: IBKR's data and the app's own sources (News: IB's providers and our feeds). Listed in
	/// the Interactive Brokers section of the menu.
	/// </summary>
	Mixed = 2,
}
