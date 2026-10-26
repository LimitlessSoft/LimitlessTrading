using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.ViewModels;

/// <summary>
/// Base for widgets whose work lives only while connected. Each connection gets a "session"
/// cancellation token; it is cancelled on disconnect, so every subscription started with it ends
/// (and frees its IB market data line).
/// </summary>
public abstract partial class ConnectedViewModelBase : ViewModelBase, IDisposable
{
	private readonly IConnectionManager _connectionManager;
	private CancellationTokenSource? _session;
	private bool _disposed;

	protected ConnectedViewModelBase(IConnectionManager connectionManager)
	{
		_connectionManager = connectionManager;
		connectionManager.StatusChanged += OnStatusChanged;

		// Posted so the derived constructor has finished before OnConnected runs
		Dispatcher.UIThread.Post(() =>
			SetConnected(connectionManager.Status.State == ConnectionState.Connected)
		);
	}

	[ObservableProperty]
	public partial bool IsConnected { get; set; }

	[ObservableProperty]
	public partial string? ErrorMessage { get; set; }

	/// <summary>
	/// Stops the widget for good (a closed extra instance, e.g. a second chart): ends session work
	/// and stops listening to the connection.
	/// </summary>
	public virtual void Dispose()
	{
		_disposed = true;
		_connectionManager.StatusChanged -= OnStatusChanged;
		CancelSession();
		GC.SuppressFinalize(this);
	}

	protected virtual void OnConnected(CancellationToken session) { }

	protected virtual void OnDisconnected() { }

	/// <summary>Cancels and restarts session work, e.g. after the selected account changed.</summary>
	protected void RestartSession()
	{
		if (!IsConnected)
			return;

		CancelSession();
		_session = new CancellationTokenSource();
		OnConnected(_session.Token);
	}

	/// <summary>Runs <paramref name="work"/> now and then every <paramref name="interval"/> until cancelled.</summary>
	protected async Task RunPeriodicAsync(
		Func<CancellationToken, Task> work,
		TimeSpan interval,
		CancellationToken cancellationToken
	)
	{
		try
		{
			using var timer = new PeriodicTimer(interval);
			do
			{
				try
				{
					await work(cancellationToken);
					ErrorMessage = null;
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
				}
			} while (await timer.WaitForNextTickAsync(cancellationToken));
		}
		catch (OperationCanceledException) { }
	}

	private void OnStatusChanged(Contracts.Dtos.Connection.ConnectionStatusDto status) =>
		Dispatcher.UIThread.Post(() => SetConnected(status.State == ConnectionState.Connected));

	private void SetConnected(bool connected)
	{
		if (_disposed || connected == IsConnected)
			return;

		IsConnected = connected;
		CancelSession();
		if (connected)
		{
			_session = new CancellationTokenSource();
			OnConnected(_session.Token);
		}
		else
			OnDisconnected();
	}

	private void CancelSession()
	{
		_session?.Cancel();
		_session?.Dispose();
		_session = null;
	}
}
