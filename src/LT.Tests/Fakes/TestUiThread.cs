using System.Collections.Concurrent;
using Avalonia.Threading;
using LT.Tests.Fakes;

[assembly: AssemblyFixture(typeof(TestUiThread.DispatcherClaim))]

namespace LT.Tests.Fakes;

/// <summary>
/// Avalonia's <c>Dispatcher.UIThread</c> belongs to the first thread that touches it, and tests run
/// on whatever thread xUnit picks. Code that needs the UI thread (creating a view model with a
/// <c>DispatcherTimer</c>, running posted jobs) runs here instead, on one dedicated thread, so no
/// test depends on which thread got to the dispatcher first. The thread claims the dispatcher in an
/// assembly fixture, before any test runs.
/// </summary>
public static class TestUiThread
{
	private static readonly BlockingCollection<Action> Work = [];

	static TestUiThread() =>
		new Thread(() =>
		{
			foreach (var action in Work.GetConsumingEnumerable())
				action();
		})
		{
			IsBackground = true,
			Name = "Test UI thread",
		}.Start();

	/// <summary>Created by xUnit before any test runs.</summary>
	public sealed class DispatcherClaim
	{
		public DispatcherClaim() => Invoke(() => Dispatcher.UIThread);
	}

	public static void Invoke(Action action) =>
		Invoke(() =>
		{
			action();
			return true;
		});

	public static T Invoke<T>(Func<T> func)
	{
		var result = new TaskCompletionSource<T>();
		Work.Add(() =>
		{
			try
			{
				result.SetResult(func());
			}
			catch (Exception ex)
			{
				result.SetException(ex);
			}
		});
		return result.Task.GetAwaiter().GetResult();
	}
}
