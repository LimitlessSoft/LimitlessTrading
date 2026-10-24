using LT.Contracts.Constants;
using LT.Contracts.Dtos.Messages;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;

namespace LT.Domain.Managers;

/// <summary>In-memory ring buffer of app messages. Registered as a singleton (see Program.cs).</summary>
public class MessageLogManager : IMessageLogManager
{
	private readonly Lock _lock = new();
	private readonly LinkedList<MessageDto> _messages = new();

	public event Action<MessageDto>? MessageAdded;

	public IReadOnlyList<MessageDto> GetAll()
	{
		lock (_lock)
			return _messages.ToList();
	}

	public void Add(MessageSeverity severity, string source, string text)
	{
		var message = new MessageDto
		{
			Time = DateTimeOffset.Now,
			Severity = severity,
			Source = source,
			Text = text,
		};
		lock (_lock)
		{
			_messages.AddLast(message);
			while (_messages.Count > MessageLogConstants.Capacity)
				_messages.RemoveFirst();
		}
		MessageAdded?.Invoke(message);
	}

	public void Clear()
	{
		lock (_lock)
			_messages.Clear();
	}
}
