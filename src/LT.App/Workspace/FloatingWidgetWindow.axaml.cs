using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace LT.App.Workspace;

/// <summary>Detached host: one widget frame in its own window, owned by the main window.</summary>
public partial class FloatingWidgetWindow : Window
{
	public FloatingWidgetWindow()
	{
		InitializeComponent();
		AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
	}

	// Keys here never reach the main window: Ctrl+Shift+F acts on this window's widget
	private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
	{
		var ctrl =
			e.KeyModifiers.HasFlag(KeyModifiers.Control)
			|| e.KeyModifiers.HasFlag(KeyModifiers.Meta);
		if (
			e.Key != Key.F
			|| !ctrl
			|| !e.KeyModifiers.HasFlag(KeyModifiers.Shift)
			|| (Content as WidgetFrame)?.Widget is not { } widget
		)
			return;
		widget.ToggleFullscreenCommand.Execute(null);
		e.Handled = true;
	}
}
