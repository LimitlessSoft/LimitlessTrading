namespace LT.Contracts.Dtos.Flex;

/// <summary>Where the Flex import stands, for the Settings status line and the widgets' notes.</summary>
public class FlexImportStatusDto
{
	public bool IsConfigured { get; set; }
	public bool HasTradeConfirmQuery { get; set; }
	public bool IsRunning { get; set; }

	/// <summary>What runs now ("Back-filling 2023-10-27 – 2024-10-25"), null when idle.</summary>
	public string? Activity { get; set; }
	public DateTimeOffset? LastActivityImport { get; set; }
	public DateTimeOffset? LastTradeConfirmImport { get; set; }
	public DateOnly? CoveredFrom { get; set; }
	public DateOnly? CoveredTo { get; set; }
	public bool BackfillComplete { get; set; }
	public string? LastError { get; set; }
	public DateTimeOffset? LastErrorAt { get; set; }
}
