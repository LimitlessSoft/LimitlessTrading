using LT.Contracts.Dtos.Executions;

namespace LT.App.Widgets.Executions;

/// <summary>One fill in the table; its time in the display zone.</summary>
public sealed record ExecutionRowViewModel(ExecutionDto Execution, string TimeText);
