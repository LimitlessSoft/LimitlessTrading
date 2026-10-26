using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LT.App.Workspace;

/// <summary>
/// A small wedge filling a frame's top-left corner (the widget's category color). Its corner is
/// rounded by <see cref="CornerRadius"/>'s top-left radius, bound to the frame's, so it follows
/// both the card look (rounded) and the flush / docked look (square).
/// </summary>
public class CornerMarker : Control
{
	public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<
		CornerMarker,
		IBrush?
	>(nameof(Fill));

	public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
		AvaloniaProperty.Register<CornerMarker, CornerRadius>(nameof(CornerRadius));

	static CornerMarker() => AffectsRender<CornerMarker>(FillProperty, CornerRadiusProperty);

	public IBrush? Fill
	{
		get => GetValue(FillProperty);
		set => SetValue(FillProperty, value);
	}

	public CornerRadius CornerRadius
	{
		get => GetValue(CornerRadiusProperty);
		set => SetValue(CornerRadiusProperty, value);
	}

	public override void Render(DrawingContext context)
	{
		var width = Bounds.Width;
		var height = Bounds.Height;
		if (Fill is not { } fill || width <= 0 || height <= 0)
			return;
		// The rounded corner can't take more than half of the wedge's shorter leg
		var radius = Math.Clamp(CornerRadius.TopLeft, 0, Math.Min(width, height) / 2);
		var geometry = new StreamGeometry();
		using (var g = geometry.Open())
		{
			g.BeginFigure(new Point(0, height), isFilled: true);
			g.LineTo(new Point(0, radius));
			if (radius > 0)
				g.ArcTo(
					new Point(radius, 0),
					new Size(radius, radius),
					0,
					false,
					SweepDirection.Clockwise
				);
			g.LineTo(new Point(width, 0));
			g.EndFigure(true);
		}
		context.DrawGeometry(fill, null, geometry);
	}
}
