using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LT.Indicators.Scripting;

/// <summary>
/// Puts <c>IndicatorRuntime.Checkpoint()</c> at the top of every loop body, before every
/// <c>goto</c> and at the start of every method, local function, accessor, constructor, operator
/// and lambda of a script (expression bodies become blocks), so an endless loop is stopped at
/// the time budget and deep recursion before the stack runs out — instead of hanging or
/// crashing the app. Uses the semantic model of the original tree to tell void bodies apart.
/// </summary>
internal sealed class ScriptCheckpointRewriter(SemanticModel model) : CSharpSyntaxRewriter
{
	// On a line of its own, so a #line directive after it stays at the start of its line
	private static StatementSyntax Checkpoint() =>
		SyntaxFactory
			.ParseStatement("global::LT.Indicators.Sdk.IndicatorRuntime.Checkpoint();")
			.WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

	public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
	{
		var visited = (ForStatementSyntax)base.VisitForStatement(node)!;
		return visited.WithStatement(Guard(visited.Statement));
	}

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		var visited = (ForEachStatementSyntax)base.VisitForEachStatement(node)!;
		return visited.WithStatement(Guard(visited.Statement));
	}

	public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
	{
		var visited = (ForEachVariableStatementSyntax)base.VisitForEachVariableStatement(node)!;
		return visited.WithStatement(Guard(visited.Statement));
	}

	public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
	{
		var visited = (WhileStatementSyntax)base.VisitWhileStatement(node)!;
		return visited.WithStatement(Guard(visited.Statement));
	}

	public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
	{
		var visited = (DoStatementSyntax)base.VisitDoStatement(node)!;
		return visited.WithStatement(Guard(visited.Statement));
	}

	public override SyntaxNode? VisitGotoStatement(GotoStatementSyntax node) =>
		SyntaxFactory.Block(Checkpoint(), (StatementSyntax)base.VisitGotoStatement(node)!);

	public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
	{
		var returnsVoid = ReturnsVoid(model.GetDeclaredSymbol(node));
		var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
		if (visited.Body == null && visited.ExpressionBody == null)
			return visited; // abstract / partial
		return visited
			.WithBody(GuardBody(visited.Body, visited.ExpressionBody, returnsVoid))
			.WithExpressionBody(null)
			.WithSemicolonToken(default);
	}

	public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
	{
		var returnsVoid = ReturnsVoid(model.GetDeclaredSymbol(node));
		var visited = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!;
		return visited
			.WithBody(GuardBody(visited.Body, visited.ExpressionBody, returnsVoid))
			.WithExpressionBody(null)
			.WithSemicolonToken(default);
	}

	public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
	{
		var visited = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;
		return visited
			.WithBody(GuardBody(visited.Body, visited.ExpressionBody, true))
			.WithExpressionBody(null)
			.WithSemicolonToken(default);
	}

	public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node)
	{
		var visited = (OperatorDeclarationSyntax)base.VisitOperatorDeclaration(node)!;
		return visited
			.WithBody(GuardBody(visited.Body, visited.ExpressionBody, false))
			.WithExpressionBody(null)
			.WithSemicolonToken(default);
	}

	public override SyntaxNode? VisitConversionOperatorDeclaration(
		ConversionOperatorDeclarationSyntax node
	)
	{
		var visited = (ConversionOperatorDeclarationSyntax)
			base.VisitConversionOperatorDeclaration(node)!;
		return visited
			.WithBody(GuardBody(visited.Body, visited.ExpressionBody, false))
			.WithExpressionBody(null)
			.WithSemicolonToken(default);
	}

	public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
	{
		var returnsVoid = ReturnsVoid(model.GetDeclaredSymbol(node));
		var visited = (AccessorDeclarationSyntax)base.VisitAccessorDeclaration(node)!;
		if (visited.Body == null && visited.ExpressionBody == null)
			return visited; // auto-property
		return visited
			.WithBody(GuardBody(visited.Body, visited.ExpressionBody, returnsVoid))
			.WithExpressionBody(null)
			.WithSemicolonToken(default);
	}

	// int P => expr;  →  int P { get { Checkpoint(); return expr; } }
	public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
	{
		var visited = (PropertyDeclarationSyntax)base.VisitPropertyDeclaration(node)!;
		if (visited.ExpressionBody is not { } body)
			return visited;
		return visited
			.WithExpressionBody(null)
			.WithSemicolonToken(default)
			.WithAccessorList(GetterOnly(body));
	}

	public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
	{
		var visited = (IndexerDeclarationSyntax)base.VisitIndexerDeclaration(node)!;
		if (visited.ExpressionBody is not { } body)
			return visited;
		return visited
			.WithExpressionBody(null)
			.WithSemicolonToken(default)
			.WithAccessorList(GetterOnly(body));
	}

	public override SyntaxNode? VisitParenthesizedLambdaExpression(
		ParenthesizedLambdaExpressionSyntax node
	)
	{
		var returnsVoid = ReturnsVoid(model.GetSymbolInfo(node).Symbol);
		var visited = (ParenthesizedLambdaExpressionSyntax)
			base.VisitParenthesizedLambdaExpression(node)!;
		return visited
			.WithBlock(GuardLambda(visited.Block, visited.ExpressionBody, returnsVoid))
			.WithExpressionBody(null);
	}

	public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
	{
		var returnsVoid = ReturnsVoid(model.GetSymbolInfo(node).Symbol);
		var visited = (SimpleLambdaExpressionSyntax)base.VisitSimpleLambdaExpression(node)!;
		return visited
			.WithBlock(GuardLambda(visited.Block, visited.ExpressionBody, returnsVoid))
			.WithExpressionBody(null);
	}

	public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
	{
		var visited = (AnonymousMethodExpressionSyntax)base.VisitAnonymousMethodExpression(node)!;
		return visited.WithBlock(Guard(visited.Block));
	}

	private static AccessorListSyntax GetterOnly(ArrowExpressionClauseSyntax body) =>
		SyntaxFactory.AccessorList(
			SyntaxFactory.SingletonList(
				SyntaxFactory.AccessorDeclaration(
					SyntaxKind.GetAccessorDeclaration,
					GuardBody(null, body, false)
				)
			)
		);

	private static BlockSyntax GuardLambda(
		BlockSyntax? block,
		ExpressionSyntax? expression,
		bool returnsVoid
	) =>
		block != null ? Guard(block)
		: expression is ThrowExpressionSyntax thrown
			? SyntaxFactory.Block(Checkpoint(), SyntaxFactory.ThrowStatement(thrown.Expression))
		: returnsVoid
			? SyntaxFactory.Block(Checkpoint(), SyntaxFactory.ExpressionStatement(expression!))
		: SyntaxFactory.Block(Checkpoint(), SyntaxFactory.ReturnStatement(expression!));

	private static BlockSyntax GuardBody(
		BlockSyntax? block,
		ArrowExpressionClauseSyntax? arrow,
		bool returnsVoid
	) => GuardLambda(block, arrow?.Expression, returnsVoid);

	private static BlockSyntax Guard(StatementSyntax body) =>
		body is BlockSyntax block
			? block.WithStatements(block.Statements.Insert(0, Checkpoint()))
			: SyntaxFactory.Block(Checkpoint(), body);

	private static bool ReturnsVoid(ISymbol? symbol) =>
		symbol is IMethodSymbol { ReturnsVoid: true };
}
