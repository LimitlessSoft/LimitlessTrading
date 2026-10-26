using LT.App.Stores;

namespace LT.App.Workspace;

/// <summary>
/// A widget that can join a link group: its frame shows the link button and the workspace saves
/// the group with its placement.
/// </summary>
public interface ILinkable
{
	ContractLink Link { get; }
}
