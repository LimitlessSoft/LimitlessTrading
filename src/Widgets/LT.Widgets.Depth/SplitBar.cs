using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LT.App.Widgets.Depth;

/// <summary>
/// A bar split in two: <see cref="Fraction"/> of the width from the left in <see cref="LeftFill"/>,
/// the rest in <see cref="RightFill"/>.
/// </summary>
public class SplitBar : Control
{
	public static readonly StyledProperty<double> FractionProperty = AvaloniaProperty.Register<
		SplitBar,
		double
	>(nameof(Fraction), 0.5);

	public static readonly StyledProperty<IBrush?> LeftFillProperty = AvaloniaProperty.Register<
		SplitBar,
		IBrush?
	>(nameof(LeftFill));

	public static readonly StyledProperty<IBrush?> RightFillProperty = AvaloniaProperty.Register<
		SplitBar,
		IBrush?
	>(nameof(RightFill));

	static SplitBar() =>
		AffectsRender<SplitBar>(FractionProperty, LeftFillProperty, RightFillProperty);

	public double Fraction
	{
		get => GetValue(FractionProperty);
		set => SetValue(FractionProperty, value);
	}

	public IBrush? LeftFill
	{
		get => GetValue(LeftFillProperty);
		set => SetValue(LeftFillProperty, value);
	}

	public IBrush? RightFill
	{
		get => GetValue(RightFillProperty);
		set => SetValue(RightFillProperty, value);
	}

	public override void Render(DrawingContext context)
	{
		var left = Bounds.Width * Math.Clamp(Fraction, 0, 1);
		if (left > 0 && LeftFill is { } leftFill)
			context.FillRectangle(leftFill, new Rect(0, 0, left, Bounds.Height));
		if (left < Bounds.Width && RightFill is { } rightFill)
			context.FillRectangle(rightFill, new Rect(left, 0, Bounds.Width - left, Bounds.Height));
	}
}
