using LT.Contracts.Dtos.Messages;

namespace LT.App.Widgets.Messages;

/// <summary>One log entry in the table; its time in the display zone.</summary>
public sealed record MessageRowViewModel(MessageDto Message, string TimeText);
