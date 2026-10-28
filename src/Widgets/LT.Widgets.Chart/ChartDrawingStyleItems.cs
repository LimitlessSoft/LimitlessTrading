using System.Windows.Input;
using Avalonia.Collections;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

/// <summary>A colour swatch of the drawing style bar; no colour is "Auto" (a fib level in the drawing's).</summary>
public class ChartDrawingColorItem(string? hex, ICommand? command = null)
{
	/// <summary>"#RRGGBB" / "#AARRGGBB"; null for "Auto".</summary>
	public string? Hex => hex;

	public IBrush? Brush => field ??= ToBrush(hex);

	public bool IsAuto => hex == null;

	/// <summary>Run with <see cref="Hex"/>.</summary>
	public ICommand? Command => command;

	public static IBrush? ToBrush(string? hex) =>
		hex != null && ChartDrawingHelpers.IsValidColor(hex)
			? new ImmutableSolidColorBrush(Color.Parse(ChartDrawingHelpers.NormalizeColor(hex)))
			: null;
}

/// <summary>A line width or line style the style bar offers, drawn as a short line.</summary>
public partial class ChartDrawingLineOption(
	string toolTip,
	double width,
	ChartDrawingLineStyle lineStyle,
	ICommand command
) : ObservableObject
{
	public string ToolTip => toolTip;
	public double Width => width;
	public ChartDrawingLineStyle LineStyle => lineStyle;

	/// <summary>The preview line's dashes, in multiples of its width (null: solid).</summary>
	public AvaloniaList<double>? Dashes =>
		field ??= GetDashes(lineStyle) is { } dashes ? [.. dashes] : null;

	/// <summary>Run with the option itself.</summary>
	public ICommand Command => command;

	/// <summary>The selected drawing's.</summary>
	[ObservableProperty]
	public partial bool IsActive { get; set; }

	/// <summary>Dashes and gaps in multiples of the line's width, as Avalonia pens take them.</summary>
	public static double[]? GetDashes(ChartDrawingLineStyle style) =>
		style switch
		{
			ChartDrawingLineStyle.Dashed => [4, 3],
			ChartDrawingLineStyle.Dotted => [1, 2],
			_ => null,
		};
}

/// <summary>
/// A row of the Fibonacci levels editor: the level as typed, shown or not, and its colour. Edits
/// stay here until the editor's Apply.
/// </summary>
public partial class ChartFibLevelItem : ObservableObject
{
	public ChartFibLevelItem(ChartViewModel owner, string valueText, bool isVisible, string? color)
	{
		Owner = owner;
		ValueText = valueText;
		IsVisible = isVisible;
		Colors =
		[
			new ChartDrawingColorItem(null),
			.. ChartDrawingConstants.Colors.Select(x => new ChartDrawingColorItem(x)),
		];
		// A colour not among the swatches (typed, or a default level's) is offered too
		if (
			color != null
			&& Colors.All(x => !string.Equals(x.Hex, color, StringComparison.OrdinalIgnoreCase))
		)
			Colors = [.. Colors, new ChartDrawingColorItem(color)];
		SelectedColor = Colors.First(x =>
			string.Equals(x.Hex, color, StringComparison.OrdinalIgnoreCase)
		);
	}

	public ChartViewModel Owner { get; }

	public IReadOnlyList<ChartDrawingColorItem> Colors { get; }

	[ObservableProperty]
	public partial string ValueText { get; set; }

	[ObservableProperty]
	public partial bool IsVisible { get; set; }

	[ObservableProperty]
	public partial ChartDrawingColorItem SelectedColor { get; set; }
}
