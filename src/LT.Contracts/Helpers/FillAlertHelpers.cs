using LT.Contracts.Dtos.Executions;

namespace LT.Contracts.Helpers;

public static class FillAlertHelpers
{
	/// <summary>
	/// Adds the ExecIds not in <paramref name="seen"/> yet and returns how many there were;
	/// executions without an id are ignored. Each partial fill has its own ExecId.
	/// </summary>
	public static int AddNewExecutions(ISet<string> seen, IEnumerable<ExecutionDto> executions)
	{
		var added = 0;
		foreach (var execution in executions)
			if (!string.IsNullOrEmpty(execution.ExecId) && seen.Add(execution.ExecId))
				added++;
		return added;
	}
}
