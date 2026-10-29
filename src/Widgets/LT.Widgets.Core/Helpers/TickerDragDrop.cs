using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using LT.App.Workspace;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Helpers;

namespace LT.App.Helpers;

/// <summary>
/// Dragging a ticker from a list onto a chart. <c>IsSource</c> on a list (a DataGrid, a ListBox)
/// makes its rows drag sources: a left press on a row becomes a drag once it moves past
/// <see cref="WorkspaceHelpers.IsBeyondDragThreshold"/>, carrying the contract the list's
/// <see cref="ITickerDragSource"/> view model gives for the row. A click, double-click or a
/// press on a button inside the row (Close, Cancel) never starts one. <c>Contract</c> on any other
/// control (the option chain's C / P strike halves, which are buttons) makes the control itself a
/// source of that contract, the same press-and-move away; the drag cancels the control's own click.
/// <c>IsTarget</c> makes a control take drops for its <see cref="ITickerDropTarget"/> view model;
/// while a ticker hovers it, <c>HoverText</c> holds what the drop will do, for a highlight to bind
/// to.
/// </summary>
public static class TickerDragDrop
{
	/// <summary>The dragged contract; in-process only, never offered to other applications.</summary>
	public static readonly DataFormat<ContractDto> ContractFormat =
		DataFormat.CreateInProcessFormat<ContractDto>("LimitlessTrading.Contract");

	public static readonly AttachedProperty<bool> IsSourceProperty =
		AvaloniaProperty.RegisterAttached<Control, bool>("IsSource", typeof(TickerDragDrop));

	public static readonly AttachedProperty<bool> IsTargetProperty =
		AvaloniaProperty.RegisterAttached<Control, bool>("IsTarget", typeof(TickerDragDrop));

	/// <summary>
	/// The contract a press-and-move on this control drags; null (or unresolved, ConId 0): none.
	/// </summary>
	public static readonly AttachedProperty<ContractDto?> ContractProperty =
		AvaloniaProperty.RegisterAttached<Control, ContractDto?>(
			"Contract",
			typeof(TickerDragDrop)
		);

	/// <summary>"Show AAPL" while a ticker hovers the target, null otherwise.</summary>
	public static readonly AttachedProperty<string?> HoverTextProperty =
		AvaloniaProperty.RegisterAttached<Control, string?>("HoverText", typeof(TickerDragDrop));

	private static readonly ConditionalWeakTable<Control, SourceGesture> Sources = [];

	static TickerDragDrop()
	{
		IsSourceProperty.Changed.AddClassHandler<Control>(OnIsSourceChanged);
		IsTargetProperty.Changed.AddClassHandler<Control>(OnIsTargetChanged);
		ContractProperty.Changed.AddClassHandler<Control>(OnContractChanged);
	}

	public static bool GetIsSource(Control control) => control.GetValue(IsSourceProperty);

	public static void SetIsSource(Control control, bool value) =>
		control.SetValue(IsSourceProperty, value);

	public static bool GetIsTarget(Control control) => control.GetValue(IsTargetProperty);

	public static void SetIsTarget(Control control, bool value) =>
		control.SetValue(IsTargetProperty, value);

	public static ContractDto? GetContract(Control control) => control.GetValue(ContractProperty);

	public static void SetContract(Control control, ContractDto? value) =>
		control.SetValue(ContractProperty, value);

	public static string? GetHoverText(Control control) => control.GetValue(HoverTextProperty);

	public static void SetHoverText(Control control, string? value) =>
		control.SetValue(HoverTextProperty, value);

	private static void OnIsSourceChanged(Control control, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.NewValue is true && !Sources.TryGetValue(control, out _))
			Sources.Add(control, new SourceGesture(control, ownContract: false));
		else if (e.NewValue is not true && Sources.TryGetValue(control, out var gesture))
		{
			gesture.Detach();
			Sources.Remove(control);
		}
	}

	// A recycled row's cell may lose its contract (no put at the new strike): no gesture then
	private static void OnContractChanged(Control control, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.NewValue != null && !Sources.TryGetValue(control, out _))
			Sources.Add(control, new SourceGesture(control, ownContract: true));
		else if (e.NewValue == null && Sources.TryGetValue(control, out var gesture))
		{
			gesture.Detach();
			Sources.Remove(control);
		}
	}

	private static void OnIsTargetChanged(Control control, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.NewValue is true)
		{
			DragDrop.SetAllowDrop(control, true);
			control.AddHandler(DragDrop.DragEnterEvent, OnDragOver);
			control.AddHandler(DragDrop.DragOverEvent, OnDragOver);
			control.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
			control.AddHandler(DragDrop.DropEvent, OnDrop);
		}
		else
		{
			DragDrop.SetAllowDrop(control, false);
			control.RemoveHandler(DragDrop.DragEnterEvent, OnDragOver);
			control.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
			control.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
			control.RemoveHandler(DragDrop.DropEvent, OnDrop);
			SetHoverText(control, null);
		}
	}

	private static void OnDragOver(object? sender, DragEventArgs e)
	{
		if (sender is not Control control)
			return;
		var contract =
			control.DataContext is ITickerDropTarget
				? e.DataTransfer.TryGetValue(ContractFormat)
				: null;
		e.DragEffects =
			contract != null ? e.DragEffects & DragDropEffects.Copy : DragDropEffects.None;
		SetHoverText(
			control,
			contract != null && e.DragEffects != DragDropEffects.None
				? $"Show {ContractHelpers.GetDisplayName(contract)}"
				: null
		);
		e.Handled = true;
	}

	private static void OnDragLeave(object? sender, DragEventArgs e)
	{
		if (sender is Control control)
			SetHoverText(control, null);
	}

	private static void OnDrop(object? sender, DragEventArgs e)
	{
		if (sender is not Control control)
			return;
		SetHoverText(control, null);
		e.Handled = true;
		if (
			control.DataContext is not ITickerDropTarget target
			|| e.DataTransfer.TryGetValue(ContractFormat) is not { ConId: > 0 } contract
		)
		{
			e.DragEffects = DragDropEffects.None;
			return;
		}
		e.DragEffects = DragDropEffects.Copy;
		target.DropContract(contract);
	}

	/// <summary>
	/// Press → move past the threshold → drag, on one source list (a row's contract) or one
	/// control carrying its own <c>Contract</c>.
	/// </summary>
	private sealed class SourceGesture
	{
		private readonly Control _host;
		private readonly bool _ownContract;
		private PointerPressedEventArgs? _press;
		private Point _pressPoint;
		private object? _item;

		public SourceGesture(Control host, bool ownContract)
		{
			_host = host;
			_ownContract = ownContract;
			// The grid handles its own row presses (selection); handled ones count too
			host.AddHandler(
				InputElement.PointerPressedEvent,
				OnPressed,
				RoutingStrategies.Bubble,
				handledEventsToo: true
			);
			host.AddHandler(
				InputElement.PointerMovedEvent,
				OnMoved,
				RoutingStrategies.Bubble,
				handledEventsToo: true
			);
			host.AddHandler(
				InputElement.PointerReleasedEvent,
				OnReleased,
				RoutingStrategies.Bubble,
				handledEventsToo: true
			);
			host.PointerCaptureLost += OnCaptureLost;
		}

		public void Detach()
		{
			_host.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
			_host.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
			_host.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
			_host.PointerCaptureLost -= OnCaptureLost;
			Reset();
		}

		private void OnPressed(object? sender, PointerPressedEventArgs e)
		{
			Reset();
			var point = e.GetCurrentPoint(_host);
			if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
				return;
			// A control with its own contract is the item itself, buttons included
			var item = _ownContract ? _host : GetRowItem(e.Source as Visual);
			if (item == null)
				return;
			_press = e;
			_pressPoint = point.Position;
			_item = item;
		}

		private void OnMoved(object? sender, PointerEventArgs e)
		{
			if (_press is not { } press)
				return;
			var point = e.GetCurrentPoint(_host);
			if (!point.Properties.IsLeftButtonPressed)
			{
				Reset();
				return;
			}
			var delta = point.Position - _pressPoint;
			if (!WorkspaceHelpers.IsBeyondDragThreshold(delta.X, delta.Y))
				return;
			var item = _item;
			Reset();
			if (!_ownContract)
			{
				if ((_host.DataContext as ITickerDragSource)?.GetDragContract(item) is { } row)
					_ = DragAsync(press, row);
				return;
			}
			if (GetContract(_host) is not { ConId: > 0 } contract)
				return;
			// The button under the press loses its capture, so the drop doesn't also click it
			e.Pointer.Capture(null);
			_ = DragAsync(press, contract);
		}

		private void OnReleased(object? sender, PointerReleasedEventArgs e) => Reset();

		private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e) => Reset();

		private void Reset()
		{
			_press = null;
			_item = null;
		}

		private static async Task DragAsync(PointerPressedEventArgs press, ContractDto contract)
		{
			using var data = new DataTransfer();
			data.Add(DataTransferItem.Create(ContractFormat, contract));
			try
			{
				await DragDrop.DoDragDropAsync(press, data, DragDropEffects.Copy);
			}
			catch (Exception)
			{
				// A platform without drag and drop just doesn't drag; the row stays selected
			}
		}

		// The row under the press: the outermost element between it and the list that has an
		// item of its own as DataContext. Presses on buttons, text boxes and scroll bars inside the
		// list are theirs, and headers share the list's DataContext, so neither starts a drag.
		private object? GetRowItem(Visual? source)
		{
			var listContext = _host.DataContext;
			object? item = null;
			for (
				var visual = source;
				visual != null && visual != _host;
				visual = visual.GetVisualParent()
			)
			{
				if (visual is Button or TextBox or ScrollBar or Thumb)
					return null;
				if (
					visual is StyledElement { DataContext: { } context }
					&& !ReferenceEquals(context, listContext)
				)
					item = context;
			}
			return item;
		}
	}
}
