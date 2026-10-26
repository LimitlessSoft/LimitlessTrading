namespace LT.App.Helpers;

/// <summary>
/// Holds only the newest value written from a background thread until the UI takes it - used to
/// coalesce high-frequency ticks into one UI update per flush.
/// </summary>
public sealed class LatestValue<T>
	where T : class
{
	private T? _value;

	public void Set(T value) => Interlocked.Exchange(ref _value, value);

	public T? Take() => Interlocked.Exchange(ref _value, null);
}
