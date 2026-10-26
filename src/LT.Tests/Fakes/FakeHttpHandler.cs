using System.Net;

namespace LT.Tests.Fakes;

/// <summary>Serves canned HTTP answers in order (the last one repeats) and records each request and when it came.</summary>
public class FakeHttpHandler(TimeProvider timeProvider) : HttpMessageHandler
{
	private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();
	private Func<HttpRequestMessage, HttpResponseMessage>? _last;

	public List<(Uri Uri, DateTimeOffset At, string? UserAgent)> Requests { get; } = [];

	public FakeHttpHandler Respond(string body, HttpStatusCode status = HttpStatusCode.OK)
	{
		_responses.Enqueue(_ => new HttpResponseMessage(status)
		{
			Content = new StringContent(body),
		});
		return this;
	}

	public FakeHttpHandler Throw(Exception exception)
	{
		_responses.Enqueue(_ => throw exception);
		return this;
	}

	protected override Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken
	)
	{
		Requests.Add(
			(request.RequestUri!, timeProvider.GetUtcNow(), request.Headers.UserAgent.ToString())
		);
		if (_responses.Count > 0)
			_last = _responses.Dequeue();
		return Task.FromResult(_last!(request));
	}
}
