using LT.Contracts.Dtos.Messages;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>A copied message reads as one line: time, level, source and text.</summary>
public class MessageHelpersTests
{
	[Fact]
	public void Copy_line_has_time_level_source_and_text()
	{
		var message = new MessageDto
		{
			Severity = MessageSeverity.Error,
			Source = "Chart",
			Text = "No market data permissions",
		};

		Assert.Equal(
			"14:05:03 [Error] Chart: No market data permissions",
			MessageHelpers.FormatCopyLine(message, "14:05:03")
		);
	}

	[Fact]
	public void Copy_line_without_a_source_skips_it()
	{
		var message = new MessageDto { Severity = MessageSeverity.Info, Text = "Connected" };

		Assert.Equal(
			"14:05:03 [Info] Connected",
			MessageHelpers.FormatCopyLine(message, "14:05:03")
		);
	}

	[Fact]
	public void Copy_lines_go_one_per_line()
	{
		Assert.Equal($"a{Environment.NewLine}b", MessageHelpers.JoinCopyLines(["a", "b"]));
	}
}
