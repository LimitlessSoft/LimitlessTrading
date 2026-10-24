using LT.Contracts.Dtos.Messages;
using LT.Contracts.Enums;

namespace LT.Contracts.Interfaces.IManagers;

public interface IMessageLogManager
{
	/// <summary>Raised on any thread for every new message.</summary>
	event Action<MessageDto>? MessageAdded;

	IReadOnlyList<MessageDto> GetAll();
	void Add(MessageSeverity severity, string source, string text);
	void Clear();
}
