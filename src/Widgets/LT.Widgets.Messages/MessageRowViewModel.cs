using LT.Contracts.Dtos.Messages;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Messages;

/// <summary>One log entry in the table; its time in the display zone.</summary>
public sealed record MessageRowViewModel(MessageDto Message, string TimeText)
{
	/// <summary>The row as it goes to the clipboard.</summary>
	public string CopyLine => MessageHelpers.FormatCopyLine(Message, TimeText);
}
