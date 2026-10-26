using Avalonia.Threading;

namespace LT.App.Helpers;

/// <summary>
/// Runs a redraw at most once per interval: a request when the last redraw is at least an
/// interval old redraws at once; otherwise one redraw is scheduled for when the interval is up,
/// covering every request until then. A zero interval redraws on every request. Used on the UI
/// thread; the scheduled redraw is posted back to it (<paramref name="post"/>, by default
/// <see cref="Dispatcher.UIThread"/>).
/// </summary>
public sealed class RedrawThrottle(
	TimeProvider timeProvider,
	TimeSpan interval,
	Action redraw,
	Action<Action>? post = null
) : IDisposable
{
	private readonly Action<Action> _post = post ?? (x => Dispatcher.UIThread.Post(x));
	private DateTimeOffset? _lastRedraw;
	private ITimer? _timer;
	private bool _disposed;

	public void Request()
	{
		if (_disposed || _timer != null)
			return;
		var now = timeProvider.GetUtcNow();
		var wait = _lastRedraw is { } last ? last + interval - now : TimeSpan.Zero;
		if (wait <= TimeSpan.Zero)
		{
			_lastRedraw = now;
			redraw();
			return;
		}
		_timer = timeProvider.CreateTimer(_ => _post(OnDue), null, wait, Timeout.InfiniteTimeSpan);
	}

	public void Dispose()
	{
		_disposed = true;
		_timer?.Dispose();
		_timer = null;
	}

	private void OnDue()
	{
		_timer?.Dispose();
		_timer = null;
		if (_disposed)
			return;
		_lastRedraw = timeProvider.GetUtcNow();
		redraw();
	}
}
