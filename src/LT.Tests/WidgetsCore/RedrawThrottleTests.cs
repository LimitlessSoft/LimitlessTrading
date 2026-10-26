using LT.App.Helpers;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.WidgetsCore;

public class RedrawThrottleTests
{
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 23, 15, 0, 0, TimeSpan.Zero)
	);
	private int _redraws;

	private RedrawThrottle Create(TimeSpan interval) =>
		new(_time, interval, () => _redraws++, x => x());

	[Fact]
	public void Redraws_at_once_then_at_most_once_per_interval()
	{
		using var throttle = Create(TimeSpan.FromSeconds(1));

		throttle.Request();
		Assert.Equal(1, _redraws);

		_time.Advance(TimeSpan.FromMilliseconds(200));
		throttle.Request();
		throttle.Request();
		Assert.Equal(1, _redraws);

		_time.Advance(TimeSpan.FromMilliseconds(800));
		Assert.Equal(2, _redraws);

		// Nothing requested since: no extra redraw
		_time.Advance(TimeSpan.FromSeconds(5));
		Assert.Equal(2, _redraws);

		throttle.Request();
		Assert.Equal(3, _redraws);
	}

	[Fact]
	public void A_zero_interval_redraws_on_every_request()
	{
		using var throttle = Create(TimeSpan.Zero);

		throttle.Request();
		throttle.Request();

		Assert.Equal(2, _redraws);
	}

	[Fact]
	public void A_scheduled_redraw_is_dropped_once_disposed()
	{
		var throttle = Create(TimeSpan.FromSeconds(1));
		throttle.Request();
		throttle.Request();

		throttle.Dispose();
		_time.Advance(TimeSpan.FromSeconds(2));

		Assert.Equal(1, _redraws);
	}
}
