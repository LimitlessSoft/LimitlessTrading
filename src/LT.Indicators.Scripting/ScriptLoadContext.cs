using System.Runtime.Loader;

namespace LT.Indicators.Scripting;

/// <summary>
/// One compiled script, collectible so a new version unloads the old one. Resolves nothing
/// itself: the SDK and the runtime come from the app's own load context, so a script shares the
/// app's <c>Indicator</c> type.
/// </summary>
internal sealed class ScriptLoadContext(string name)
	: AssemblyLoadContext(name, isCollectible: true);
