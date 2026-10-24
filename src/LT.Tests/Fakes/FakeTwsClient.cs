using System.Reflection;
using System.Threading.Channels;
using TwsApi;

namespace LT.Tests.Fakes;

/// <summary>
/// ITwsClient for LT.Ibkr tests: account summary values come from <see cref="AccountSummary"/>;
/// every other member throws.
/// </summary>
public class FakeTwsClient : DispatchProxy
{
	public Channel<AccountValue> AccountSummary { get; } = Channel.CreateUnbounded<AccountValue>();
	public int AccountSummaryRequests { get; private set; }

	public static (ITwsClient Client, FakeTwsClient Fake) Create()
	{
		var client = Create<ITwsClient, FakeTwsClient>();
		return (client, (FakeTwsClient)(object)client);
	}

	protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
	{
		if (targetMethod?.Name != nameof(ITwsClient.SubscribeAccountSummaryAsync))
			throw new NotSupportedException(targetMethod?.Name);

		AccountSummaryRequests++;
		return AccountSummary.Reader.ReadAllAsync((CancellationToken)args![2]!);
	}
}
