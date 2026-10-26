using LT.App.Helpers;

namespace LT.Tests.WidgetsCore;

public class ZonedTextTests
{
	private string? _text;
	private string _zone = "A";

	[Fact]
	public void Rebuild_formats_the_text_again()
	{
		var text = new ZonedText(x => _text = x);
		text.Set(() => $"Loaded in {_zone}");

		_zone = "B";
		text.Rebuild();

		Assert.Equal("Loaded in B", _text);
	}

	[Fact]
	public void A_cleared_text_stays_empty_on_rebuild()
	{
		var text = new ZonedText(x => _text = x);
		text.Set(() => $"Loaded in {_zone}");

		text.Clear();
		text.Rebuild();

		Assert.Null(_text);
	}

	[Fact]
	public void An_appended_suffix_keeps_following_the_zone()
	{
		var text = new ZonedText(x => _text = x);
		text.Set(() => $"Live in {_zone}");
		text.Append(" · unavailable");

		_zone = "B";
		text.Rebuild();

		Assert.Equal("Live in B · unavailable", _text);
	}
}
