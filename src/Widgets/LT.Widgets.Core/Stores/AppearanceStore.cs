using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Stores;

/// <summary>How the app looks (Settings → Appearance); follows saved settings while it runs.</summary>
public partial class AppearanceStore : ObservableObject
{
	public AppearanceStore(ISettingsManager settingsManager)
	{
		LayoutStyle = settingsManager.GetAppSettings().LayoutStyle;
		settingsManager.AppSettingsChanged += settings =>
			Dispatcher.UIThread.Post(() => LayoutStyle = settings.LayoutStyle);
	}

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsFlush))]
	public partial LayoutStyle LayoutStyle { get; set; }

	/// <summary>Widgets fill the window edge to edge (no rounded tiles or gaps).</summary>
	public bool IsFlush => LayoutStyle == LayoutStyle.Flush;
}
