using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LT.App.Helpers;

namespace LT.App.Controls;

/// <summary>
/// Scales its content by <see cref="UiScale.Factor"/> and follows it live: a layout transform, so
/// the content is laid out at the scaled size (not stretched), stays vector-sharp, and pointer
/// positions and hit tests go through the transform. <see cref="AppWindow"/>'s template wraps
/// the title bar and content in one; popups (menus, flyouts, tooltips, drop-downs) follow their
/// placement target's scale (<c>Popup.InheritsTransform</c>, set app-wide in App.axaml).
/// </summary>
public class UiScaleHost : LayoutTransformControl
{
	protected override Type StyleKeyOverride => typeof(LayoutTransformControl);

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		UiScale.Changed += Apply;
		Apply();
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		UiScale.Changed -= Apply;
		base.OnDetachedFromVisualTree(e);
	}

	private void Apply()
	{
		var factor = UiScale.Factor;
		// None at 100 %: no transform in the way of anything
		LayoutTransform = factor.Equals(1) ? null : new ScaleTransform(factor, factor);
	}
}
