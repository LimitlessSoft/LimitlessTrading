namespace LT.Contracts.Enums;

/// <summary>What a news source can serve; the news manager skips sources that can't serve a request.</summary>
[Flags]
public enum NewsSourceCapabilities
{
	None = 0,

	/// <summary>Streams new headlines.</summary>
	Live = 1,

	/// <summary>Past headlines, with paging back.</summary>
	History = 2,

	/// <summary>Full article text for a headline.</summary>
	Articles = 4,

	/// <summary>Headlines of one contract.</summary>
	BySymbol = 8,

	/// <summary>Headlines not tied to a contract (a provider's whole feed).</summary>
	BroadTape = 16,
}
