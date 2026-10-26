using LT.App.Helpers;
using LT.Contracts.Dtos.MarketDepth;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.MarketDepth;

namespace LT.App.Widgets.Depth;

/// <summary>
/// The Depth widget's one depth subscription, read on the thread pool. It runs only while there is
/// a request, the connection session is live and the widget is open: closing the widget cancels
/// it at once, so its slot (one of IB's three) is free for TWS and other apps. No UI thread here;
/// <paramref name="onError"/> is called on the thread pool with the failed stream's token.
/// </summary>
public sealed class DepthFeed(
	IMarketDepthManager marketDepthManager,
	Action<Exception, CancellationToken> onError
) : IDisposable
{
	private SubscribeOrderBookRequest? _request;
	private CancellationToken _session = new(true);
	private CancellationTokenSource? _stream;

	public bool IsOpen { get; private set; } = true;

	/// <summary>A subscription is running (or starting).</summary>
	public bool IsSubscribed => _stream != null;

	/// <summary>The newest book; replaced per subscription, so an old stream can't write into it.</summary>
	public LatestValue<OrderBookDto> Pending { get; private set; } = new();

	/// <summary>
	/// What to show: a request (null for nothing) within a connection session (cancelled when
	/// disconnected). Replaces the running subscription.
	/// </summary>
	public void Subscribe(SubscribeOrderBookRequest? request, CancellationToken session)
	{
		_request = request;
		_session = session;
		Restart();
	}

	/// <summary>Closed: cancels the subscription; opened again: resumes the last request.</summary>
	public void SetOpen(bool isOpen)
	{
		if (isOpen == IsOpen)
			return;
		IsOpen = isOpen;
		Restart();
	}

	public void Dispose()
	{
		_request = null;
		Cancel();
	}

	private void Restart()
	{
		Cancel();
		if (_request is not { } request || !IsOpen || _session.IsCancellationRequested)
			return;
		_stream = CancellationTokenSource.CreateLinkedTokenSource(_session);
		Pending = new LatestValue<OrderBookDto>();
		var pending = Pending;
		var token = _stream.Token;
		_ = Task.Run(() => ReadAsync(request, pending, token), CancellationToken.None);
	}

	private async Task ReadAsync(
		SubscribeOrderBookRequest request,
		LatestValue<OrderBookDto> pending,
		CancellationToken cancellationToken
	)
	{
		try
		{
			await foreach (
				var book in marketDepthManager
					.SubscribeOrderBookAsync(request, cancellationToken)
					.ConfigureAwait(false)
			)
				pending.Set(book);
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			if (!cancellationToken.IsCancellationRequested)
				onError(ex, cancellationToken);
		}
	}

	private void Cancel()
	{
		_stream?.Cancel();
		_stream?.Dispose();
		_stream = null;
	}
}
