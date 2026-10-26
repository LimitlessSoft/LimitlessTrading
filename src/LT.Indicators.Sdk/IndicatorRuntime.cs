using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace LT.Indicators.Sdk;

/// <summary>Thrown when an indicator runs past its time budget.</summary>
public sealed class IndicatorTimeoutException(TimeSpan budget)
	: Exception($"The indicator ran longer than {budget.TotalSeconds:0.#} s and was stopped.");

/// <summary>
/// The time and stack guard of a running indicator. The script compiler calls
/// <see cref="Checkpoint"/> at the top of every loop body and method in a script; scripts don't
/// call it themselves.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class IndicatorRuntime
{
	[ThreadStatic]
	private static long _deadline;

	[ThreadStatic]
	private static TimeSpan _budget;

	/// <summary>Throws once the budget is spent, or before the stack runs out (deep recursion).</summary>
	public static void Checkpoint()
	{
		RuntimeHelpers.EnsureSufficientExecutionStack();
		if (_deadline != 0 && Stopwatch.GetTimestamp() > _deadline)
			throw new IndicatorTimeoutException(_budget);
	}

	internal static IDisposable Start(TimeSpan budget)
	{
		var previous = (_deadline, _budget);
		_budget = budget;
		_deadline =
			budget > TimeSpan.Zero
				? Stopwatch.GetTimestamp() + (long)(budget.TotalSeconds * Stopwatch.Frequency)
				: 0;
		return new Scope(previous);
	}

	private sealed class Scope((long Deadline, TimeSpan Budget) previous) : IDisposable
	{
		public void Dispose() => (_deadline, _budget) = previous;
	}
}
