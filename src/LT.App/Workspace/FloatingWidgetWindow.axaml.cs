using Avalonia.Controls;

namespace LT.App.Workspace;

/// <summary>Detached host: one widget frame in its own window, owned by the main window.</summary>
public partial class FloatingWidgetWindow : Window
{
	public FloatingWidgetWindow() => InitializeComponent();
}
