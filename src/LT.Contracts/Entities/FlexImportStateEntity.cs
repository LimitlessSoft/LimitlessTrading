namespace LT.Contracts.Entities;

/// <summary>What the Flex import has fetched so far (flex-import.json), so a restart doesn't download it again.</summary>
public class FlexImportStateEntity
{
	/// <summary>The Activity query the covered range belongs to; another query starts over.</summary>
	public string ActivityQueryId { get; set; } = string.Empty;

	/// <summary>Report dates imported from the Activity query, oldest and newest.</summary>
	public DateOnly? CoveredFrom { get; set; }
	public DateOnly? CoveredTo { get; set; }

	/// <summary>Back-fill reached the account's start (an empty or unavailable year, or dateOpened).</summary>
	public bool BackfillComplete { get; set; }
	public DateTimeOffset? LastActivitySuccess { get; set; }
	public DateTimeOffset? LastActivityAttempt { get; set; }
	public DateTimeOffset? LastTradeConfirmSuccess { get; set; }
	public DateTimeOffset? LastTradeConfirmAttempt { get; set; }
	public string? LastError { get; set; }
	public DateTimeOffset? LastErrorAt { get; set; }
}
