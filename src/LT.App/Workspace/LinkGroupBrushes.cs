using Avalonia.Media;
using LT.Contracts.Enums;

namespace LT.App.Workspace;

/// <summary>The color of each link group: the frame's link button and its menu.</summary>
public static class LinkGroupBrushes
{
	public static IBrush None { get; } = new SolidColorBrush(Color.Parse("#8A93A3"));
	public static IBrush Red { get; } = new SolidColorBrush(Color.Parse("#EF5350"));
	public static IBrush Green { get; } = new SolidColorBrush(Color.Parse("#66BB6A"));
	public static IBrush Blue { get; } = new SolidColorBrush(Color.Parse("#42A5F5"));
	public static IBrush Yellow { get; } = new SolidColorBrush(Color.Parse("#FFCA28"));
	public static IBrush Orange { get; } = new SolidColorBrush(Color.Parse("#FFA726"));
	public static IBrush Purple { get; } = new SolidColorBrush(Color.Parse("#AB47BC"));
	public static IBrush Cyan { get; } = new SolidColorBrush(Color.Parse("#26C6DA"));
	public static IBrush Pink { get; } = new SolidColorBrush(Color.Parse("#EC407A"));
	public static IBrush Lime { get; } = new SolidColorBrush(Color.Parse("#D4E157"));
	public static IBrush White { get; } = new SolidColorBrush(Color.Parse("#E0E0E0"));

	public static IBrush Get(LinkGroup group) =>
		group switch
		{
			LinkGroup.Red => Red,
			LinkGroup.Green => Green,
			LinkGroup.Blue => Blue,
			LinkGroup.Yellow => Yellow,
			LinkGroup.Orange => Orange,
			LinkGroup.Purple => Purple,
			LinkGroup.Cyan => Cyan,
			LinkGroup.Pink => Pink,
			LinkGroup.Lime => Lime,
			LinkGroup.White => White,
			_ => None,
		};
}
