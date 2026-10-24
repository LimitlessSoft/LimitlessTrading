using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Stores;
using LT.Contracts.Enums;

namespace LT.App.Workspace;

/// <summary>
/// One widget instance: what it is, the view model it shows and where it currently lives.
/// <see cref="Dock"/> is null while the widget is closed.
/// </summary>
public partial class WidgetItem : ObservableObject
{
	public WidgetItem(string id, WorkspaceTool tool, object content)
	{
		Id = id;
		Tool = tool;
		Content = content;
		Icon = Geometry.Parse(WidgetCatalog.GetIcon(tool));
		Link = (content as ILinkable)?.Link;
		if (Link != null)
			Link.PropertyChanged += (_, _) =>
			{
				OnPropertyChanged(nameof(LinkBrush));
				OnPropertyChanged(nameof(LinkToolTip));
			};
	}

	/// <summary>Instance id; unique in the workspace, kept in the saved layout.</summary>
	public string Id { get; }
	public WorkspaceTool Tool { get; }
	public string Title => WidgetCatalog.GetTitle(Tool);
	public Geometry Icon { get; }
	public string ToolTip =>
		WidgetCatalog.GetFocusGesture(Tool) is { } gesture ? $"{Title} ({gesture})" : Title;

	/// <summary>The widget's view model; WidgetViewFactory creates the matching view.</summary>
	public object Content { get; }

	/// <summary>Link group membership; null for widgets that can't be linked.</summary>
	public ContractLink? Link { get; }
	public bool IsLinkable => Link != null;
	public IBrush LinkBrush => LinkGroupBrushes.Get(Link?.Group ?? LinkGroup.None);
	public string LinkToolTip =>
		Link?.Group is { } group && group != LinkGroup.None
			? $"Linked: {group} group"
			: "Not linked";

	/// <summary>Set by the workspace that owns the item.</summary>
	public WorkspaceViewModel? Workspace { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(
		nameof(IsOpen),
		nameof(IsCenter),
		nameof(IsLeft),
		nameof(IsRight),
		nameof(IsFloating),
		nameof(IsSide),
		nameof(CanPin)
	)]
	public partial WidgetDock? Dock { get; set; }

	/// <summary>Where the widget was last open, so reopening puts it back there.</summary>
	public WidgetDock? LastDock { get; set; }

	/// <summary>Shown over the whole window (or its floating window maximized).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(FullscreenToolTip), nameof(CanPin))]
	public partial bool IsFullscreen { get; set; }

	/// <summary>Side widgets show the pin (not while fullscreen).</summary>
	public bool CanPin => IsSide && !IsFullscreen;

	/// <summary>Side widgets: unpinned ones collapse as soon as you click or type elsewhere.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PinToolTip))]
	public partial bool IsPinned { get; set; } = true;

	public string PinToolTip =>
		IsPinned
			? "Pinned: stays open (click to unpin)"
			: "Unpinned: collapses when you click elsewhere";

	/// <summary>Last clicked or typed in (Ctrl+Shift+F's target); shown with a light frame.</summary>
	[ObservableProperty]
	public partial bool IsCurrent { get; set; }

	public string FullscreenToolTip =>
		IsFullscreen ? "Restore (Ctrl+Shift+F)" : "Fullscreen (Ctrl+Shift+F)";

	/// <summary>Side widgets only: this one is expanded in its side panel.</summary>
	[ObservableProperty]
	public partial bool IsActive { get; set; }

	public bool IsOpen => Dock != null;
	public bool IsCenter => Dock == WidgetDock.Center;
	public bool IsLeft => Dock == WidgetDock.Left;
	public bool IsRight => Dock == WidgetDock.Right;
	public bool IsFloating => Dock == WidgetDock.Floating;
	public bool IsSide => IsLeft || IsRight;

	// Last floating window bounds (device pixels for position, DIPs for size)
	public double? FloatX { get; set; }
	public double? FloatY { get; set; }
	public double? FloatWidth { get; set; }
	public double? FloatHeight { get; set; }

	/// <summary>Raised when a floating widget should come to the front.</summary>
	public event Action<WidgetItem>? FocusRequested;

	public void RequestFocus() => FocusRequested?.Invoke(this);

	/// <summary>Strip icon: expand or collapse in the side panel.</summary>
	[RelayCommand]
	private void Toggle() => Workspace?.ToggleSide(this);

	[RelayCommand]
	private void Close() => Workspace?.Close(this);

	[RelayCommand]
	private void ToggleFullscreen() => Workspace?.ToggleFullscreen(this);

	[RelayCommand]
	private void TogglePin() => IsPinned = !IsPinned;

	[RelayCommand]
	private void SetLinkGroup(LinkGroup group)
	{
		if (Link != null)
			Link.Group = group;
	}
}
