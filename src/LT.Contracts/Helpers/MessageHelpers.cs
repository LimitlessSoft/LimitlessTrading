using LT.Contracts.Dtos.Messages;

namespace LT.Contracts.Helpers;

public static class MessageHelpers
{
	/// <summary>
	/// One message as a readable line for the clipboard: "time [Level] Source: text", the time
	/// already formatted in the display zone (<paramref name="timeText"/>); no source part when
	/// the message has none.
	/// </summary>
	public static string FormatCopyLine(MessageDto message, string timeText)
	{
		var source = string.IsNullOrWhiteSpace(message.Source)
			? string.Empty
			: $"{message.Source}: ";
		return $"{timeText} [{message.Severity}] {source}{message.Text}";
	}

	/// <summary>Several lines as one clipboard text, one per line, in the order given.</summary>
	public static string JoinCopyLines(IEnumerable<string> lines) =>
		string.Join(Environment.NewLine, lines);
}
