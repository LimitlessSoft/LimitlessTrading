using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using LT.App.ViewModels;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Messages;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Messages;

public partial class MessagesViewModel : ViewModelBase
{
	private readonly IMessageLogManager _messageLogManager;

	public MessagesViewModel(IMessageLogManager messageLogManager)
	{
		_messageLogManager = messageLogManager;
		foreach (var message in messageLogManager.GetAll().Reverse())
			Messages.Add(message);
		messageLogManager.MessageAdded += message =>
			Dispatcher.UIThread.Post(() =>
			{
				Messages.Insert(0, message);
				while (Messages.Count > MessageLogConstants.Capacity)
					Messages.RemoveAt(Messages.Count - 1);
			});
	}

	/// <summary>Newest first.</summary>
	public ObservableCollection<MessageDto> Messages { get; } = [];

	[RelayCommand]
	private void Clear()
	{
		_messageLogManager.Clear();
		Messages.Clear();
	}
}
