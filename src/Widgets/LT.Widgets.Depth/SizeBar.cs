using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LT.App.Widgets.Depth;

/// <summary>A bar behind a ladder row: <see cref="Fraction"/> of the width, from the right edge.</summary>
public class SizeBar : Control
{
	public static readonly StyledProperty<double> FractionProperty = AvaloniaProperty.Register<
		SizeBar,
		double
	>(nameof(Fraction));

	public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<
		SizeBar,
		IBrush?
	>(nameof(Fill));

	static SizeBar() => AffectsRender<SizeBar>(FractionProperty, FillProperty);

	public double Fraction
	{
		get => GetValue(FractionProperty);
		set => SetValue(FractionProperty, value);
	}

	public IBrush? Fill
	{
		get => GetValue(FillProperty);
		set => SetValue(FillProperty, value);
	}

	public override void Render(DrawingContext context)
	{
		var width = Bounds.Width * Math.Clamp(Fraction, 0, 1);
		if (width <= 0 || Fill is not { } fill)
			return;
		context.FillRectangle(fill, new Rect(Bounds.Width - width, 0, width, Bounds.Height));
	}
}
