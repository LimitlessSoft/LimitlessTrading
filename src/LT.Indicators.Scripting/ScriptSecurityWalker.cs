using LT.Contracts.Dtos.Indicators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LT.Indicators.Scripting;

/// <summary>
/// Rejects what an indicator script must not use: every name, expression and attribute is
/// resolved and checked against the banned namespaces and types (also as generic arguments,
/// array elements and the type an expression produces), plus syntax that has no business in an
/// indicator (unsafe code, extern methods, lock, finalizers, __makeref …). A guard against
/// mistakes and casual misuse, not a sandbox: see docs/indicators.md.
/// </summary>
internal sealed class ScriptSecurityWalker(SemanticModel model) : CSharpSyntaxWalker
{
	// Namespace (and everything under it) → why
	private static readonly (string Namespace, string Reason)[] BannedNamespaces =
	[
		("System.IO", "files"),
		("System.Net", "network"),
		("System.Reflection", "reflection"),
		("System.Runtime", "runtime internals and native code"),
		("System.Threading", "threads and tasks"),
		("System.Diagnostics", "processes and diagnostics"),
		("System.Security", "security APIs"),
		("System.Resources", "resources"),
		("System.Timers", "timers"),
		("System.CodeDom", "code generation"),
		("Microsoft.Win32", "the Windows registry"),
		("Internal", "runtime internals"),
	];

	private static readonly Dictionary<string, string> BannedTypes = new()
	{
		["System.Environment"] = "the environment",
		["System.AppDomain"] = "the app domain",
		["System.AppContext"] = "the app context",
		["System.Activator"] = "reflection",
		["System.Type"] = "reflection",
		["System.TypedReference"] = "runtime internals",
		["System.ArgIterator"] = "runtime internals",
		["System.RuntimeTypeHandle"] = "reflection",
		["System.RuntimeMethodHandle"] = "reflection",
		["System.RuntimeFieldHandle"] = "reflection",
		["System.ModuleHandle"] = "reflection",
		["System.Delegate"] = "reflection",
		["System.MulticastDelegate"] = "reflection",
		["System.GC"] = "the garbage collector",
		["System.Console"] = "the console",
		["System.Buffer"] = "raw memory",
	};

	private readonly HashSet<(int Line, string Name)> _reported = [];

	public List<IndicatorDiagnosticDto> Diagnostics { get; } = [];

	public override void Visit(SyntaxNode? node)
	{
		if (node == null)
			return;
		switch (node)
		{
			case ExpressionSyntax expression:
				CheckExpression(expression);
				break;
			case AttributeSyntax attribute:
				CheckSymbol(attribute, model.GetSymbolInfo(attribute).Symbol);
				break;
			case UsingDirectiveSyntax { Name: { } name }:
				CheckSymbol(name, model.GetSymbolInfo(name).Symbol);
				break;
		}
		if (GetBannedSyntax(node) is { } construct)
			Report(node, construct, $"{construct} can't be used in an indicator script.");
		base.Visit(node);
	}

	private void CheckExpression(ExpressionSyntax expression)
	{
		var info = model.GetSymbolInfo(expression);
		CheckSymbol(expression, info.Symbol);
		foreach (var candidate in info.CandidateSymbols)
			CheckSymbol(expression, candidate);
		var type = model.GetTypeInfo(expression);
		CheckSymbol(expression, type.Type);
		CheckSymbol(expression, type.ConvertedType);
	}

	private void CheckSymbol(SyntaxNode node, ISymbol? symbol)
	{
		if (symbol == null)
			return;
		if (GetBannedReason(symbol) is { } banned)
			Report(
				node,
				banned.Name,
				$"'{banned.Name}' can't be used in an indicator script ({banned.Reason})."
			);
	}

	private void Report(SyntaxNode node, string name, string message)
	{
		var span = node.GetLocation().GetMappedLineSpan();
		var line = span.StartLinePosition.Line + 1;
		if (!_reported.Add((line, name)))
			return;
		Diagnostics.Add(
			new IndicatorDiagnosticDto
			{
				Line = line,
				Column = span.StartLinePosition.Character + 1,
				Message = message,
			}
		);
	}

	private static (string Name, string Reason)? GetBannedReason(ISymbol symbol)
	{
		switch (symbol)
		{
			case INamespaceSymbol ns:
				return GetBannedNamespace(ns) is { } reason ? (ns.ToDisplayString(), reason) : null;
			case ITypeSymbol type:
				return GetBannedType(type);
			case IMethodSymbol { IsExtern: true } method:
				return (method.Name, "native code");
		}
		if (symbol is IMethodSymbol { ReducedFrom: { } extension })
			symbol = extension;
		if (symbol.ContainingType is { } containing && GetBannedType(containing) is { } banned)
			return ($"{banned.Name}.{symbol.Name}", banned.Reason);
		return symbol switch
		{
			IMethodSymbol method => GetBannedType(method.ReturnType),
			IPropertySymbol property => GetBannedType(property.Type),
			IFieldSymbol field => GetBannedType(field.Type),
			ILocalSymbol local => GetBannedType(local.Type),
			IParameterSymbol parameter => GetBannedType(parameter.Type),
			_ => null,
		};
	}

	private static (string Name, string Reason)? GetBannedType(ITypeSymbol? type)
	{
		switch (type)
		{
			case null:
				return null;
			case IArrayTypeSymbol array:
				return GetBannedType(array.ElementType);
			case IPointerTypeSymbol:
			case IFunctionPointerTypeSymbol:
				return ("pointers", "unsafe code");
			case IDynamicTypeSymbol:
				return ("dynamic", "late binding");
		}
		if (type is INamedTypeSymbol named)
			foreach (var argument in named.TypeArguments)
				if (GetBannedType(argument) is { } inner)
					return inner;
		for (var current = type; current != null; current = current.ContainingType)
		{
			var name = current.OriginalDefinition.ToDisplayString(
				SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(
					SymbolDisplayGlobalNamespaceStyle.Omitted
				)
			);
			var key = name.Split('<')[0];
			if (BannedTypes.TryGetValue(key, out var reason))
				return (key, reason);
			if (
				current.ContainingType == null
				&& current.ContainingNamespace is { } ns
				&& GetBannedNamespace(ns) is { } nsReason
			)
				return (key, nsReason);
		}
		return null;
	}

	private static string? GetBannedNamespace(INamespaceSymbol ns)
	{
		if (ns.IsGlobalNamespace)
			return null;
		var name = ns.ToDisplayString();
		foreach (var (banned, reason) in BannedNamespaces)
			if (name == banned || name.StartsWith(banned + ".", StringComparison.Ordinal))
				return reason;
		return null;
	}

	private static string? GetBannedSyntax(SyntaxNode node) =>
		node switch
		{
			UnsafeStatementSyntax => "unsafe",
			FixedStatementSyntax => "fixed",
			PointerTypeSyntax or FunctionPointerTypeSyntax => "Pointers",
			LockStatementSyntax => "lock",
			DestructorDeclarationSyntax => "A finalizer",
			MakeRefExpressionSyntax => "__makeref",
			RefTypeExpressionSyntax => "__reftype",
			RefValueExpressionSyntax => "__refvalue",
			ExternAliasDirectiveSyntax => "extern alias",
			AwaitExpressionSyntax => "await",
			MemberDeclarationSyntax member
				when member.Modifiers.Any(SyntaxKind.ExternKeyword)
					|| member.Modifiers.Any(SyntaxKind.UnsafeKeyword) =>
				"An extern or unsafe member",
			LocalFunctionStatementSyntax local
				when local.Modifiers.Any(SyntaxKind.ExternKeyword)
					|| local.Modifiers.Any(SyntaxKind.UnsafeKeyword)
					|| local.Modifiers.Any(SyntaxKind.AsyncKeyword) =>
				"An extern, unsafe or async function",
			MethodDeclarationSyntax method when method.Modifiers.Any(SyntaxKind.AsyncKeyword) =>
				"An async method",
			_ => null,
		};
}
