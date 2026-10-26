namespace LT.Contracts.Requests.Flex;

/// <summary>One Flex statement: the query's own period, or <see cref="From"/>–<see cref="To"/> (at most 365 days).</summary>
public class FlexStatementRequest
{
	public string Token { get; set; } = string.Empty;
	public string QueryId { get; set; } = string.Empty;
	public DateOnly? From { get; set; }
	public DateOnly? To { get; set; }
}
