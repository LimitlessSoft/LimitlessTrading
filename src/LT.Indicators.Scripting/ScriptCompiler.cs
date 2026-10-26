using System.Security.Cryptography;
using System.Text;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Indicators;
using LT.Indicators.Sdk;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LT.Indicators.Scripting;

/// <summary>A compiled script (or why it didn't compile).</summary>
public sealed class ScriptCompileResult
{
	public bool Success => Assembly != null;
	public byte[]? Assembly { get; init; }
	public byte[]? Pdb { get; init; }

	/// <summary>Full name of the <c>Indicator</c> subclass to run.</summary>
	public string? TypeName { get; init; }

	public List<IndicatorDiagnosticDto> Diagnostics { get; init; } = [];
}

/// <summary>
/// Compiles one script file with Roslyn (<see cref="CSharpCompilation"/>, not the scripting
/// API) against an allowlist of references — the SDK and a minimal part of the runtime — then
/// rejects banned APIs (<see cref="ScriptSecurityWalker"/>) and adds the time / stack guard
/// (<see cref="ScriptCheckpointRewriter"/>) before emitting it with a PDB, so runtime errors
/// carry the file's line numbers.
/// </summary>
public static class ScriptCompiler
{
	// The runtime assemblies a script may see; everything else is not referenced at all
	private static readonly string[] RuntimeAssemblies =
	[
		"System.Private.CoreLib.dll",
		"System.Runtime.dll",
		"System.Collections.dll",
		"System.Linq.dll",
	];

	private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(() =>
	{
		var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
		return
		[
			.. RuntimeAssemblies
				.Select(x => Path.Combine(runtime, x))
				.Where(File.Exists)
				.Select(x => MetadataReference.CreateFromFile(x)),
			MetadataReference.CreateFromFile(typeof(Indicator).Assembly.Location),
		];
	});

	// Debug: unoptimised IL keeps exact IL offsets, so a runtime error names the right line.
	// Script code is mostly glue; the heavy lifting is in the SDK, which is optimised
	private static readonly CSharpCompilationOptions Options = new(
		OutputKind.DynamicallyLinkedLibrary,
		optimizationLevel: OptimizationLevel.Debug,
		allowUnsafe: false,
		nullableContextOptions: NullableContextOptions.Disable,
		concurrentBuild: false
	);

	/// <summary>Hash of a source and the SDK it is compiled against (a new SDK recompiles).</summary>
	public static string GetHash(string source)
	{
		var sdk = typeof(Indicator).Assembly.ManifestModule.ModuleVersionId;
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(source + "\n" + sdk));
		return Convert.ToHexString(bytes);
	}

	public static ScriptCompileResult Compile(
		string fileName,
		string source,
		CancellationToken cancellationToken = default
	)
	{
		// The name goes into #line directives as it is
		if (fileName.IndexOfAny(['"', '\\', '\n', '\r']) >= 0)
			return Failed(
				"Rename the file: its name can't contain quotes, backslashes or line breaks."
			);
		if (source.Length > IndicatorConstants.MaxScriptLength)
			return Failed(
				$"The file is larger than {IndicatorConstants.MaxScriptLength / 1024} KB."
			);

		var hash = GetHash(source)[..12];
		var (tree, typeName) = ScriptSource.Build(fileName, source, "S" + hash);
		var compilation = CSharpCompilation.Create(
			$"LTScript_{hash}",
			[ScriptSource.GlobalUsings, tree],
			References.Value,
			Options
		);

		var diagnostics = compilation
			.GetDiagnostics(cancellationToken)
			.Where(x => x.Severity >= DiagnosticSeverity.Warning)
			.Select(x => ToDto(x, fileName))
			.Where(x => x != null)
			.Select(x => x!)
			.Take(IndicatorConstants.MaxDiagnostics)
			.ToList();
		if (diagnostics.Any(x => x.IsError))
			return new ScriptCompileResult { Diagnostics = diagnostics };

		var model = compilation.GetSemanticModel(tree);
		var walker = new ScriptSecurityWalker(model);
		walker.Visit(tree.GetRoot(cancellationToken));
		if (walker.Diagnostics.Count > 0)
			return new ScriptCompileResult
			{
				Diagnostics = [.. walker.Diagnostics.Concat(diagnostics)],
			};

		string? typeError = null;
		typeName ??= FindIndicatorType(compilation, out typeError);
		if (typeName == null)
			return new ScriptCompileResult
			{
				Diagnostics = [new IndicatorDiagnosticDto { Message = typeError! }, .. diagnostics],
			};

		var guarded = CSharpSyntaxTree.Create(
			(CSharpSyntaxNode)
				new ScriptCheckpointRewriter(model).Visit(tree.GetRoot(cancellationToken)),
			ScriptSource.ParseOptions,
			tree.FilePath,
			Encoding.UTF8
		);
		compilation = compilation.ReplaceSyntaxTree(tree, guarded);

		using var assembly = new MemoryStream();
		using var pdb = new MemoryStream();
		var emitted = compilation.Emit(
			assembly,
			pdb,
			options: new Microsoft.CodeAnalysis.Emit.EmitOptions(
				debugInformationFormat: Microsoft
					.CodeAnalysis
					.Emit
					.DebugInformationFormat
					.PortablePdb
			),
			cancellationToken: cancellationToken
		);
		if (!emitted.Success)
			return new ScriptCompileResult
			{
				Diagnostics =
				[
					.. emitted
						.Diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error)
						.Select(x => ToDto(x, fileName) ?? ToDto(x, null)!)
						.Take(IndicatorConstants.MaxDiagnostics),
				],
			};

		return new ScriptCompileResult
		{
			Assembly = assembly.ToArray(),
			Pdb = pdb.ToArray(),
			TypeName = typeName,
			Diagnostics = diagnostics,
		};
	}

	// A file without top-level statements must declare exactly one runnable Indicator subclass
	private static string? FindIndicatorType(CSharpCompilation compilation, out string? error)
	{
		var baseType = compilation.GetTypeByMetadataName(typeof(Indicator).FullName!);
		var found = GetTypes(compilation.Assembly.GlobalNamespace)
			.Where(x => !x.IsAbstract && x.TypeKind == TypeKind.Class && Derives(x, baseType))
			.ToList();
		error = found.Count switch
		{
			0 => "The file has no statements and no class deriving from Indicator.",
			> 1 => "The file declares more than one Indicator class; keep one per file.",
			_ => null,
		};
		if (found.Count != 1)
			return null;
		if (
			!found[0]
				.InstanceConstructors.Any(x =>
					x.Parameters.Length == 0 && x.DeclaredAccessibility == Accessibility.Public
				)
		)
		{
			error = $"{found[0].Name} needs a public constructor without parameters.";
			return null;
		}
		return GetMetadataName(found[0]);
	}

	private static string GetMetadataName(INamedTypeSymbol type)
	{
		var name = type.MetadataName;
		for (var outer = type.ContainingType; outer != null; outer = outer.ContainingType)
			name = outer.MetadataName + "+" + name;
		var ns = type.ContainingNamespace;
		return ns is { IsGlobalNamespace: false } ? $"{ns.ToDisplayString()}.{name}" : name;
	}

	private static IEnumerable<INamedTypeSymbol> GetTypes(INamespaceOrTypeSymbol container)
	{
		foreach (var member in container.GetMembers())
			if (member is INamespaceSymbol ns)
				foreach (var type in GetTypes(ns))
					yield return type;
			else if (member is INamedTypeSymbol type)
			{
				yield return type;
				foreach (var nested in GetTypes(type))
					yield return nested;
			}
	}

	private static bool Derives(INamedTypeSymbol type, INamedTypeSymbol? baseType)
	{
		for (var current = type.BaseType; current != null; current = current.BaseType)
			if (SymbolEqualityComparer.Default.Equals(current, baseType))
				return true;
		return false;
	}

	// Diagnostics in the file keep its line / column; the generated wrapper's own (null file)
	// only matter when they are errors
	private static IndicatorDiagnosticDto? ToDto(Diagnostic diagnostic, string? fileName)
	{
		var span = diagnostic.Location.GetMappedLineSpan();
		var inFile =
			diagnostic.Location.IsInSource && span.HasMappedPath
				? span.Path == fileName
				: diagnostic.Location.IsInSource
					&& diagnostic.Location.SourceTree?.FilePath == fileName;
		if (fileName != null && !inFile && diagnostic.Severity != DiagnosticSeverity.Error)
			return null;
		return new IndicatorDiagnosticDto
		{
			Line = inFile ? span.StartLinePosition.Line + 1 : 0,
			Column = inFile ? span.StartLinePosition.Character + 1 : 0,
			Message = $"{diagnostic.GetMessage()} ({diagnostic.Id})",
			IsError = diagnostic.Severity == DiagnosticSeverity.Error,
		};
	}

	private static ScriptCompileResult Failed(string message) =>
		new() { Diagnostics = [new IndicatorDiagnosticDto { Message = message }] };
}
