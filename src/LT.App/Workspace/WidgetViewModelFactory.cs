using LT.Contracts.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace LT.App.Workspace;

/// <summary>
/// Creates a widget's view model from DI when an instance is spawned, so several charts each get
/// their own. Registered as a singleton in Program.cs.
/// </summary>
public class WidgetViewModelFactory(IServiceProvider services)
{
	public object Create(WorkspaceTool tool) =>
		services.GetRequiredService(WidgetCatalog.Get(tool).ViewModelType);
}
