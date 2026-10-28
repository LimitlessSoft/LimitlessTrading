using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.App.Workspace;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.ViewModels;

/// <summary>
/// Base for widgets whose work lives only while connected. Each connection gets a "session"
/// cancellation token; it is cancelled on disconnect, so every subscription started with it ends
/// (and frees its IB market data line).
/// <para>
/// Market data streams (quote lines, depth) live in a narrower "streams" scope: connected
/// <em>and</em> shown (<see cref="IWidgetVisibility"/>). A widget in an inactive view, a collapsed
/// side or bottom panel or a minimized window gets <see cref="OnStreamsStopped"/> and keeps its
/// data; <see cref="OnStreamsStarted"/> runs again when it is shown.
/// </para>
/// </summary>
public abstract partial class ConnectedViewModelBase : ViewModelBase, IDisposable, IWidgetVisibility
{
	private readonly IConnectionManager _connectionManager;
	private CancellationTokenSource? _session;
	private CancellationTokenSource? _streams;
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

	/// <summary>On screen, as the workspace last said; true until told otherwise.</summary>
	[ObservableProperty]
	public partial bool IsShown { get; private set; } = true;

	/// <summary>Connected and shown: streams may run (<see cref="StreamsToken"/>).</summary>
	protected bool IsStreaming => _streams != null;

	/// <summary>Cancelled when the widget is hidden or disconnected; already cancelled while not streaming.</summary>
	protected CancellationToken StreamsToken => _streams?.Token ?? new CancellationToken(true);

	/// <summary>
	/// Stops the widget for good (a closed extra instance, e.g. a second chart): ends session work
	/// and stops listening to the connection.
	/// </summary>
	public virtual void Dispose()
	{
		_disposed = true;
		_connectionManager.StatusChanged -= OnStatusChanged;
		CancelStreams();
		CancelSession();
		GC.SuppressFinalize(this);
	}

	/// <summary>The workspace shows or hides the widget (UI thread).</summary>
	public void SetShown(bool isShown)
	{
		if (_disposed || isShown == IsShown)
			return;
		IsShown = isShown;
		if (isShown)
		{
			OnShownChanged();
			StartStreams();
		}
		else
		{
			StopStreams();
			OnShownChanged();
		}
	}

	protected virtual void OnConnected(CancellationToken session) { }

	protected virtual void OnDisconnected() { }

	/// <summary>
	/// Connected and shown (after <see cref="OnConnected"/>): start the market data streams, with
	/// <paramref name="streams"/> (cancelled when hidden or disconnected).
	/// </summary>
	protected virtual void OnStreamsStarted(CancellationToken streams) { }

	/// <summary>
	/// <see cref="IsShown"/> changed, connected or not: after the streams stopped when hidden,
	/// before they start when shown.
	/// </summary>
	protected virtual void OnShownChanged() { }

	/// <summary>Hidden or disconnecting (before <see cref="OnDisconnected"/>): the streams' token is cancelled; tidy up, keep the data.</summary>
	protected virtual void OnStreamsStopped() { }

	/// <summary>Cancels and restarts session work, e.g. after the selected account changed.</summary>
	protected void RestartSession()
	{
		if (!IsConnected)
			return;

		StopStreams();
		CancelSession();
		_session = new CancellationTokenSource();
		OnConnected(_session.Token);
		StartStreams();
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
		StopStreams();
		CancelSession();
		if (connected)
		{
			_session = new CancellationTokenSource();
			OnConnected(_session.Token);
			StartStreams();
		}
		else
			OnDisconnected();
	}

	private void StartStreams()
	{
		if (_streams != null || _session is not { } session || !IsShown)
			return;
		_streams = CancellationTokenSource.CreateLinkedTokenSource(session.Token);
		OnStreamsStarted(_streams.Token);
	}

	private void StopStreams()
	{
		if (_streams == null)
			return;
		CancelStreams();
		OnStreamsStopped();
	}

	private void CancelStreams()
	{
		_streams?.Cancel();
		_streams?.Dispose();
		_streams = null;
	}

	private void CancelSession()
	{
		_session?.Cancel();
		_session?.Dispose();
		_session = null;
	}
}
