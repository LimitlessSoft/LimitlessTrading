using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Messages;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Messages;

public partial class MessagesViewModel : ViewModelBase, IDisposable
{
	private readonly IMessageLogManager _messageLogManager;
	private readonly DisplayTimeStore _displayTime;

	public MessagesViewModel(IMessageLogManager messageLogManager, DisplayTimeStore displayTime)
	{
		_messageLogManager = messageLogManager;
		_displayTime = displayTime;
		ShowAll();
		displayTime.ZoneChanged += ShowAll;
		messageLogManager.MessageAdded += OnMessageAdded;
	}

	/// <summary>Newest first.</summary>
	public ObservableCollection<MessageRowViewModel> Messages { get; } = [];

	[RelayCommand]
	private void Clear()
	{
		_messageLogManager.Clear();
		Messages.Clear();
	}

	/// <summary>
	/// The clipboard text for the selected rows: one readable line each, in the table's order
	/// (newest first) whatever order they were selected in; null when none is selected.
	/// </summary>
	public string? GetCopyText(IEnumerable<MessageRowViewModel> selected)
	{
		var rows = selected.ToHashSet();
		if (rows.Count == 0)
			return null;
		return MessageHelpers.JoinCopyLines(Messages.Where(rows.Contains).Select(x => x.CopyLine));
	}

	/// <summary>The widget is closed: stops listening to the log and the display zone.</summary>
	public void Dispose()
	{
		_displayTime.ZoneChanged -= ShowAll;
		_messageLogManager.MessageAdded -= OnMessageAdded;
		GC.SuppressFinalize(this);
	}

	private void OnMessageAdded(MessageDto message) =>
		Dispatcher.UIThread.Post(() =>
		{
			Messages.Insert(0, ToRow(message));
			while (Messages.Count > MessageLogConstants.Capacity)
				Messages.RemoveAt(Messages.Count - 1);
		});

	// Also on a zone change: every time formatted again
	private void ShowAll()
	{
		Messages.Clear();
		foreach (var message in _messageLogManager.GetAll().Reverse())
			Messages.Add(ToRow(message));
	}

	private MessageRowViewModel ToRow(MessageDto message) =>
		new(message, _displayTime.Format(message.Time));
}
