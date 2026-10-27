using Avalonia.Controls;
using Avalonia.Media.TextFormatting;
using LT.App.Helpers;

namespace LT.App.Controls;

/// <summary>
/// A TextBlock whose text is sensitive (an account id, a balance, P&amp;L, a quantity): in privacy
/// mode it draws <see cref="Sensitive.Mask"/> instead. <see cref="TextBlock.Text"/> and its binding
/// stay as they are; only the drawn layout changes, live on every switch. Styled as a TextBlock.
/// </summary>
public class SensitiveTextBlock : TextBlock
{
	public SensitiveTextBlock() => Sensitive.Track(this);

	protected override Type StyleKeyOverride => typeof(TextBlock);

	/// <summary>What is drawn: the text, or the mask while privacy mode is on.</summary>
	public string? ShownText => Sensitive.Apply(Text);

	protected override TextLayout CreateTextLayout(string? text) =>
		base.CreateTextLayout(Sensitive.Apply(text));

	/// <summary>Lays the text out again after privacy mode switched.</summary>
	internal void Refresh()
	{
		InvalidateTextLayout();
		InvalidateMeasure();
	}
}
