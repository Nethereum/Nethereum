using System.Security.Cryptography;
using System.Text;
using CodeQuality.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuality.Core.Parsing;

public static class CloneHasher
{
    // Convenience overload: parses, then delegates. The host uses the root-based overload so a
    // file is parsed once rather than once per method.
    public static string? Hash(string sourceText, MethodMetrics method, int minStatements) =>
        Hash(CSharpSyntaxTree.ParseText(sourceText).GetRoot(), method, minStatements);

    public static string? Hash(SyntaxNode root, MethodMetrics method, int minStatements)
    {
        if (method.StatementCount < minStatements) return null;

        var declaration = root.DescendantNodes()
            .OfType<BaseMethodDeclarationSyntax>()
            .FirstOrDefault(m => root.SyntaxTree.GetLineSpan(m.Span).StartLinePosition.Line + 1 == method.Line);

        var body = MethodBodySyntax.BodyOf(declaration);
        if (body is null) return null;

        var normalised = Normalise(body);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalised)));
    }

    static string Normalise(SyntaxNode body)
    {
        var stripped = new LiteralNormaliser().Visit(body)!;
        return stripped.NormalizeWhitespace().ToFullString();
    }

    public static IReadOnlyList<CloneGroup> Group(IEnumerable<(string Hash, MethodMetrics Method)> hashed) =>
        hashed
            .GroupBy(x => x.Hash)
            .Where(g => g.Count() > 1)
            .Select(g => new CloneGroup(
                Hash: g.Key,
                StatementCount: g.Max(x => x.Method.StatementCount),
                Members: g.Select(x => x.Method).ToList()))
            .OrderByDescending(g => g.Members.Count * g.StatementCount)
            .ToList();

    // SyntaxNode.WithoutTrivia() only clears the leading trivia of a node's first token and the
    // trailing trivia of its last token — trivia (including comments) on internal tokens survives
    // untouched. Stripping every token's trivia here, rather than the node's, is what makes a
    // trailing "// explanation" comment disappear from the hash.
    sealed class LiteralNormaliser : CSharpSyntaxRewriter
    {
        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            var normalised = token.IsKind(SyntaxKind.StringLiteralToken)
                ? SyntaxFactory.Literal("S")
                : token;

            return normalised
                .WithLeadingTrivia(SyntaxFactory.TriviaList())
                .WithTrailingTrivia(SyntaxFactory.TriviaList());
        }
    }
}
