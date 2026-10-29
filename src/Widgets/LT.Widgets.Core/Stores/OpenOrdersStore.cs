using Avalonia.Threading;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Stores;

/// <summary>
/// The working orders of every account and client, read once for every widget that shows them
/// (the charts' order lines), so ten charts cost one request. Read while at least one widget
/// <see cref="Watch"/>es and the app is connected: every
/// <see cref="AccountConstants.OrdersRefreshInterval"/>, and
/// <see cref="OrderConstants.OrdersChangedDebounce"/> after this app's orders changed
/// (<see cref="IOrderManager.OrdersChanged"/>: a status, a cancel, a new price) or
/// <see cref="Refresh"/> asked. Disconnected, or with nobody watching, the list is empty: live
/// orders only, never a snapshot.
/// </summary>
public sealed class OpenOrdersStore
{
	private readonly IOrderManager _orderManager;
	private readonly TimeProvider _timeProvider;
	private bool _connected;
	private int _watchers;
	private CancellationTokenSource? _run;

	// Completed to read again before the next interval; replaced by the loop once it is heard
	private volatile TaskCompletionSource _wake = NewWake();

	public OpenOrdersStore(
		IOrderManager orderManager,
		IConnectionManager connectionManager,
		TimeProvider timeProvider
	)
	{
		_orderManager = orderManager;
		_timeProvider = timeProvider;
		_connected = connectionManager.Status.State == ConnectionState.Connected;
		connectionManager.StatusChanged += status =>
			Dispatcher.UIThread.Post(() =>
			{
				_connected = status.State == ConnectionState.Connected;
				Update();
			});
		orderManager.OrdersChanged += Refresh;
	}

	/// <summary>The last orders read (UI thread); empty while not read.</summary>
	public IReadOnlyList<OpenOrderDto> Orders { get; private set; } = [];

	/// <summary>Raised on the UI thread after <see cref="Orders"/> was read again (or cleared).</summary>
	public event Action? Changed;

	/// <summary>
	/// Keeps the orders read until the returned handle is disposed (UI thread); several widgets
	/// share one reading.
	/// </summary>
	public IDisposable Watch()
	{
		_watchers++;
		Update();
		return new Watcher(this);
	}

	/// <summary>Reads the orders again soon, from any thread (e.g. after an order was changed).</summary>
	public void Refresh() => _wake.TrySetResult();

	private void Unwatch()
	{
		_watchers--;
		Update();
	}

	// UI thread: reading runs while watched and connected; otherwise nothing is shown
	private void Update()
	{
		var run = _watchers > 0 && _connected;
		if (run == (_run != null))
			return;
		if (run)
		{
			_run = new CancellationTokenSource();
			_ = RunAsync(_run.Token);
			return;
		}
		_run!.Cancel();
		_run.Dispose();
		_run = null;
		if (Orders.Count == 0)
			return;
		Orders = [];
		Changed?.Invoke();
	}

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		try
		{
			while (true)
			{
				try
				{
					var orders = await _orderManager.GetOpenOrdersAsync(null, cancellationToken);
					Dispatcher.UIThread.Post(() =>
					{
						// Stopped meanwhile (disconnected, nobody watching): stays cleared
						if (cancellationToken.IsCancellationRequested)
							return;
						Orders = orders;
						Changed?.Invoke();
					});
				}
				catch (Exception) when (!cancellationToken.IsCancellationRequested)
				{
					// The last orders stay until the next read works
				}

				var wake = _wake;
				await Task.WhenAny(
					wake.Task,
					Task.Delay(
						AccountConstants.OrdersRefreshInterval,
						_timeProvider,
						cancellationToken
					)
				);
				cancellationToken.ThrowIfCancellationRequested();
				if (!wake.Task.IsCompleted)
					continue;
				// One read for a burst of changes
				_wake = NewWake();
				await Task.Delay(
					OrderConstants.OrdersChangedDebounce,
					_timeProvider,
					cancellationToken
				);
			}
		}
		catch (OperationCanceledException) { }
	}

	private static TaskCompletionSource NewWake() =>
		new(TaskCreationOptions.RunContinuationsAsynchronously);

	private sealed class Watcher(OpenOrdersStore store) : IDisposable
	{
		private bool _disposed;

		public void Dispose()
		{
			if (_disposed)
				return;
			_disposed = true;
			store.Unwatch();
		}
	}
}
