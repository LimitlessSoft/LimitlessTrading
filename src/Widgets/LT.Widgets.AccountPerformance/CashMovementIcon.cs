using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LT.Contracts.Enums;

namespace LT.App.Widgets.AccountPerformance;

/// <summary>The charts' cash movement badge as a control, for the calendar and the legend.</summary>
public class CashMovementIcon : Control
{
	public static readonly StyledProperty<CashMovementKind> KindProperty =
		AvaloniaProperty.Register<CashMovementIcon, CashMovementKind>(nameof(Kind));

	static CashMovementIcon()
	{
		AffectsRender<CashMovementIcon>(KindProperty);
		WidthProperty.OverrideDefaultValue<CashMovementIcon>(12);
		HeightProperty.OverrideDefaultValue<CashMovementIcon>(12);
	}

	public CashMovementKind Kind
	{
		get => GetValue(KindProperty);
		set => SetValue(KindProperty, value);
	}

	public override void Render(DrawingContext context)
	{
		var size = Bounds.Size;
		ChartDrawing.DrawCashMovementBadge(
			context,
			new Point(size.Width / 2, size.Height / 2),
			Kind,
			Math.Min(size.Width, size.Height) / 2 - 0.5
		);
	}
}
