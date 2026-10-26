using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Messages;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Messages;

public partial class MessagesViewModel : ViewModelBase
{
	private readonly IMessageLogManager _messageLogManager;
	private readonly DisplayTimeStore _displayTime;

	public MessagesViewModel(IMessageLogManager messageLogManager, DisplayTimeStore displayTime)
	{
		_messageLogManager = messageLogManager;
		_displayTime = displayTime;
		ShowAll();
		displayTime.ZoneChanged += ShowAll;
		messageLogManager.MessageAdded += message =>
			Dispatcher.UIThread.Post(() =>
			{
				Messages.Insert(0, ToRow(message));
				while (Messages.Count > MessageLogConstants.Capacity)
					Messages.RemoveAt(Messages.Count - 1);
			});
	}

	/// <summary>Newest first.</summary>
	public ObservableCollection<MessageRowViewModel> Messages { get; } = [];

	[RelayCommand]
	private void Clear()
	{
		_messageLogManager.Clear();
		Messages.Clear();
	}

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
