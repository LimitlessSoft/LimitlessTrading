using Avalonia.Controls;
using Avalonia.Interactivity;
using LT.App.Widgets.Gateway;

namespace LT.App.Widgets.ConnectionBar;

public partial class ConnectionBarView : UserControl
{
	public ConnectionBarView() => InitializeComponent();

	private void OnClick(object? sender, RoutedEventArgs e)
	{
		if (TopLevel.GetTopLevel(this) is Window owner)
			_ = GatewayWindow.ShowAsync(owner);
	}
}
