using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuality.Core.Parsing;

internal static class MethodWalker
{
    static readonly SyntaxKind[] NestingKinds =
    {
        SyntaxKind.IfStatement, SyntaxKind.ForStatement, SyntaxKind.ForEachStatement,
        SyntaxKind.WhileStatement, SyntaxKind.DoStatement, SyntaxKind.SwitchStatement,
        SyntaxKind.TryStatement, SyntaxKind.LockStatement, SyntaxKind.UsingStatement,
    };

    static readonly SyntaxKind[] ScopeBoundaryKinds =
    {
        SyntaxKind.LocalFunctionStatement, SyntaxKind.SimpleLambdaExpression,
        SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.AnonymousMethodExpression,
    };

    internal static int MaxNesting(SyntaxNode body) => Depth(body, 0);

    static int Depth(SyntaxNode node, int current)
    {
        var deepest = current;
        foreach (var child in node.ChildNodes())
        {
            // A local function or lambda opens its own scope: its nesting ranks that
            // scope, not the method that merely contains its declaration.
            if (ScopeBoundaryKinds.Contains(child.Kind()))
                continue;

            var next = NestingKinds.Contains(child.Kind()) ? current + 1 : current;
            deepest = Math.Max(deepest, Depth(child, next));
        }
        return deepest;
    }

    internal static int StatementCount(SyntaxNode body) =>
        body.DescendantNodes().OfType<StatementSyntax>().Count(s => s is not BlockSyntax);
}
