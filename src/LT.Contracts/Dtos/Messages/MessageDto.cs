using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Messages;

public class MessageDto
{
	public DateTimeOffset Time { get; set; }
	public MessageSeverity Severity { get; set; }
	public string Source { get; set; } = string.Empty;
	public string Text { get; set; } = string.Empty;
}
