using LT.App.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace LT.App.Widgets.PriceAlerts;

/// <summary>
/// The fired price alerts, topmost and not owned (it shows over every window, whichever is
/// focused or minimized). Opened by the main window when an alert fires and closed by it once
/// none is left; closing it dismisses every alert, which stops the alarm.
/// </summary>
public partial class PriceAlertPopupWindow : AppWindow
{
	public PriceAlertPopupWindow() => InitializeComponent();

	public static PriceAlertPopupWindow Open()
	{
		var viewModel = Program.Services.GetRequiredService<PriceAlertPopupViewModel>();
		var window = new PriceAlertPopupWindow { DataContext = viewModel };
		window.Closing += (_, _) => viewModel.DismissAllCommand.Execute(null);
		window.Closed += (_, _) => viewModel.Dispose();
		window.Show();
		return window;
	}
}
