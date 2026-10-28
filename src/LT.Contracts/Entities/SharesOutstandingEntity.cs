namespace LT.Contracts.Entities;

/// <summary>
/// A cached SEC lookup of one ticker; <see cref="Shares"/> null means the SEC had nothing usable
/// (cached too, for a shorter time).
/// </summary>
public class SharesOutstandingEntity
{
	public long? Shares { get; set; }
	public DateOnly? AsOf { get; set; }
	public DateTimeOffset FetchedAt { get; set; }

	/// <summary>
	/// <see cref="Constants.SecConstants.SharesLookupVersion"/> when it was looked up (0 in entries
	/// from before it existed): a "nothing usable" from an older lookup is asked again.
	/// </summary>
	public int LookupVersion { get; set; }
}
