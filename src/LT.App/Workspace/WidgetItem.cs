using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.Contracts.Enums;

namespace LT.App.Workspace;

/// <summary>
/// One widget instance: what it is, the view model it shows and where it currently lives.
/// <see cref="Dock"/> is null while the widget is closed.
/// </summary>
public partial class WidgetItem(string id, WorkspaceTool tool, object content) : ObservableObject
{
	/// <summary>Instance id; unique in the workspace, kept in the saved layout.</summary>
	public string Id => id;
	public WorkspaceTool Tool => tool;
	public string Title => WidgetCatalog.GetTitle(tool);
	public Geometry Icon { get; } = Geometry.Parse(WidgetCatalog.GetIcon(tool));
	public string ToolTip => $"{Title} (Ctrl+{(int)tool + 1})";

	/// <summary>The widget's view model; WidgetViewFactory creates the matching view.</summary>
	public object Content => content;

	/// <summary>Set by the workspace that owns the item.</summary>
	public WorkspaceViewModel? Workspace { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(
		nameof(IsOpen),
		nameof(IsCenter),
		nameof(IsLeft),
		nameof(IsRight),
		nameof(IsFloating),
		nameof(IsSide)
	)]
	public partial WidgetDock? Dock { get; set; }

	/// <summary>Where the widget was last open, so reopening puts it back there.</summary>
	public WidgetDock? LastDock { get; set; }

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
	private void Collapse() => Workspace?.Collapse(this);

	[RelayCommand]
	private void Close() => Workspace?.Close(this);
}
