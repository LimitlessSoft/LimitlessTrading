using System.Text;
using LT.Contracts.Constants;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LT.Indicators.Scripting;

/// <summary>
/// Turns a script file into the C# that is compiled. A file with top-level statements becomes
/// the <c>Compute()</c> body of a generated <c>Indicator</c> subclass (its using directives stay
/// on top, types it declares go next to the class); a file that is one expression becomes one
/// <c>Plot</c> of it; a file without statements is compiled as it is and must declare a class
/// deriving from <c>Indicator</c>. <c>#line</c> directives keep every diagnostic and stack
/// frame on the file's own line numbers.
/// </summary>
internal static class ScriptSource
{
	public const string GeneratedNamespace = "LT.Indicators.Scripts";
	public const string GeneratedClassName = "GeneratedIndicator";

	// Calls that already draw: a one-line script made of one of these is a statement
	private static readonly HashSet<string> AuthoringMethods =
	[
		"Plot",
		"Histogram",
		"Band",
		"Markers",
		"Level",
	];

	public static CSharpParseOptions ParseOptions { get; } =
		new(LanguageVersion.Latest, DocumentationMode.None, SourceCodeKind.Regular);

	/// <summary>The usings every script gets (the editor project has the same).</summary>
	public static SyntaxTree GlobalUsings { get; } =
		Parse(
			string.Concat(IndicatorConstants.ScriptUsings.Select(x => $"global using {x};\n"))
				+ $"global using static {IndicatorConstants.ScriptStaticUsing};\n",
			"GlobalUsings.g.cs"
		);

	// With an encoding: the PDB needs one to embed the source's checksum
	public static SyntaxTree Parse(string text, string path) =>
		CSharpSyntaxTree.ParseText(SourceText.From(text, Encoding.UTF8), ParseOptions, path);

	/// <summary>
	/// The tree to compile and, for a generated class, its full name (null: find the
	/// <c>Indicator</c> subclass the file declares).
	/// </summary>
	public static (SyntaxTree Tree, string? TypeName) Build(
		string fileName,
		string source,
		string namespaceSuffix
	)
	{
		var parsed = Parse(source, fileName);
		var root = parsed.GetCompilationUnitRoot();
		var expression = GetSingleExpression(source, root);
		var statements = root.Members.OfType<GlobalStatementSyntax>().ToList();
		if (expression == null && statements.Count == 0)
			return (parsed, null);

		var text = parsed.GetText();
		var name = Path.GetFileNameWithoutExtension(fileName);
		var ns = $"{GeneratedNamespace}.{namespaceSuffix}";
		var output = new StringBuilder();
		foreach (var directive in root.Externs)
			AppendMapped(output, text, fileName, directive);
		foreach (var directive in root.Usings)
			AppendMapped(output, text, fileName, directive);
		output.Append("#line default\n");
		output.Append($"namespace {ns}\n{{\n");
		output.Append(
			$"public sealed class {GeneratedClassName} : global::LT.Indicators.Sdk.Indicator\n{{\n"
		);
		output.Append(
			$"public {GeneratedClassName}() {{ Name = {SymbolDisplay.FormatLiteral(name, true)}; }}\n"
		);
		output.Append("protected override void Compute()\n{\n");
		if (expression is { } single)
		{
			var (code, offset) = single;
			var line = text.Lines.GetLinePosition(offset);
			output.Append($"#line {line.Line + 1} \"{fileName}\"\n");
			output.Append(new string(' ', line.Character));
			output.Append($"Plot({SymbolDisplay.FormatLiteral(name, true)}, {code});\n");
		}
		else
			foreach (var statement in statements)
				AppendMapped(output, text, fileName, statement);
		output.Append("#line default\n}\n}\n");
		foreach (var member in root.Members.Where(x => x is not GlobalStatementSyntax))
			AppendMapped(output, text, fileName, member);
		output.Append("#line default\n}\n");

		var tree = Parse(output.ToString(), fileName);
		return (tree, $"{ns}.{GeneratedClassName}");
	}

	// "Ta.Sma(Close, 20)" (an optional ; after it): drawn as one line. A call of Plot & co. is a
	// statement, not a value. Returns the expression's text and where it starts in the file
	private static (string Code, int Offset)? GetSingleExpression(
		string source,
		CompilationUnitSyntax root
	)
	{
		if (root.Members.Any(x => x is not GlobalStatementSyntax))
			return null;
		var start = root.Usings.Count > 0 ? root.Usings[^1].Span.End : 0;
		var body = source[start..].Trim();
		if (body.EndsWith(';'))
			body = body[..^1].TrimEnd();
		if (body.Length == 0)
			return null;
		var expression = SyntaxFactory.ParseExpression(body, options: ParseOptions);
		if (
			expression.ContainsDiagnostics
			|| expression.FullSpan.Length != body.Length
			|| expression is AssignmentExpressionSyntax
			|| expression
				is InvocationExpressionSyntax { Expression: IdentifierNameSyntax identifier }
				&& AuthoringMethods.Contains(identifier.Identifier.Text)
		)
			return null;
		var bodyStart = source.IndexOf(body, start, StringComparison.Ordinal);
		return (expression.ToString(), Math.Max(0, bodyStart) + expression.SpanStart);
	}

	private static void AppendMapped(
		StringBuilder output,
		SourceText text,
		string fileName,
		SyntaxNode node
	)
	{
		var position = text.Lines.GetLinePosition(node.SpanStart);
		output.Append($"#line {position.Line + 1} \"{fileName}\"\n");
		output.Append(new string(' ', position.Character));
		output.Append(text.ToString(node.Span));
		output.Append('\n');
	}
}
