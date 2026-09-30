using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuality.Core.Parsing;

internal static class MethodBodySyntax
{
    internal static SyntaxNode? BodyOf(BaseMethodDeclarationSyntax? method) =>
        (SyntaxNode?)method?.Body ?? method?.ExpressionBody;

    internal static SyntaxNode? BodyOf(AccessorDeclarationSyntax accessor) =>
        (SyntaxNode?)accessor.Body ?? accessor.ExpressionBody;
}
