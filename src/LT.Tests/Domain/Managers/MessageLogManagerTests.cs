using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Domain.Managers;

namespace LT.Tests.Domain.Managers;

public class MessageLogManagerTests
{
	[Fact]
	public void Keeps_only_the_newest_messages()
	{
		var manager = new MessageLogManager();

		for (var i = 0; i < MessageLogConstants.Capacity + 10; i++)
			manager.Add(MessageSeverity.Info, "test", i.ToString());

		var all = manager.GetAll();
		Assert.Equal(MessageLogConstants.Capacity, all.Count);
		Assert.Equal("10", all[0].Text);
	}
}
