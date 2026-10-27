using LT.App.Stores;
using LT.App.Widgets.Messages;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// Copying selected messages gives one readable line per row, in the table's order (newest first)
/// whatever order the rows were selected in; nothing selected copies nothing.
/// </summary>
public class MessagesViewModelCopyTests
{
	private readonly TestCache _cache = new();
	private readonly MessageLogManager _log = new();

	private MessagesViewModel Create() =>
		new(
			_log,
			new DisplayTimeStore(
				new SettingsManager(_cache.Settings, new FakeIbkrApiClient()),
				_cache.Time
			)
		);

	[Fact]
	public void Selected_rows_copy_newest_first_whatever_the_selection_order()
	{
		_log.Add(MessageSeverity.Info, "Connection", "Connected");
		_log.Add(MessageSeverity.Warning, "Market data", "Line limit reached");
		_log.Add(MessageSeverity.Error, "Chart", "No permissions");
		var messages = Create();
		var newest = messages.Messages[0];
		var oldest = messages.Messages[2];

		var text = messages.GetCopyText([oldest, newest]);

		Assert.Equal(
			$"{newest.TimeText} [Error] Chart: No permissions{Environment.NewLine}"
				+ $"{oldest.TimeText} [Info] Connection: Connected",
			text
		);
	}

	[Fact]
	public void Nothing_selected_copies_nothing()
	{
		_log.Add(MessageSeverity.Info, "Connection", "Connected");

		Assert.Null(Create().GetCopyText([]));
	}
}
