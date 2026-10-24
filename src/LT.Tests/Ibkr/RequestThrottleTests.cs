using System.Diagnostics;
using LT.Ibkr;

namespace LT.Tests.Ibkr;

public class RequestThrottleTests
{
	[Fact]
	public async Task Requests_above_the_limit_wait_for_the_window()
	{
		var throttle = new RequestThrottle(3, TimeSpan.FromMilliseconds(300));
		var stopwatch = Stopwatch.StartNew();

		for (var i = 0; i < 3; i++)
			await throttle.WaitAsync(TestContext.Current.CancellationToken);
		Assert.True(stopwatch.ElapsedMilliseconds < 200);

		await throttle.WaitAsync(TestContext.Current.CancellationToken);
		Assert.True(stopwatch.ElapsedMilliseconds >= 250);
	}
}
