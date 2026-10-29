namespace LT.Contracts.Enums;

/// <summary>What a news widget shows; saved with its placement.</summary>
public enum NewsMode
{
	/// <summary>Headlines of the link group's contract: history, then live.</summary>
	Linked = 0,

	/// <summary>Every headline of the selected providers' broad tapes, live.</summary>
	All = 1,

	/// <summary>The local news archive searched for the typed words; no live headlines.</summary>
	Search = 2,
}
