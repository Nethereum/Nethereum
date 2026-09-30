using CodeQuality.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuality.Core.Parsing;

internal static class CommentBlockExtractor
{
    internal static IReadOnlyList<CommentBlock> Extract(string filePath, SyntaxNode root)
    {
        var comments = root.DescendantTrivia()
            .Where(IsComment)
            .OrderBy(t => t.SpanStart)
            .ToList();

        var blocks = new List<CommentBlock>();
        var current = new List<SyntaxTrivia>();

        foreach (var trivia in comments)
        {
            if (current.Count > 0 && !IsContiguous(current[^1], trivia))
            {
                blocks.Add(Build(filePath, root, current));
                current = new List<SyntaxTrivia>();
            }
            current.Add(trivia);
        }

        if (current.Count > 0) blocks.Add(Build(filePath, root, current));
        return blocks;
    }

    static bool IsComment(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);

    static bool IsContiguous(SyntaxTrivia previous, SyntaxTrivia next)
    {
        if (KindOf(previous) != KindOf(next)) return false;

        // Comments attach to code as leading or trailing trivia of a specific
        // token. Two comments belong together only if they sit in that same
        // attached run; line proximity by itself says nothing about whether
        // they attach to the same code, so it is checked after this.
        if (!AttachedToSameRun(previous, next)) return false;

        var tree = previous.SyntaxTree!;
        var previousEnd = tree.GetLineSpan(previous.Span).EndLinePosition.Line;
        var nextStart = tree.GetLineSpan(next.Span).StartLinePosition.Line;
        return nextStart - previousEnd <= 1;
    }

    static bool AttachedToSameRun(SyntaxTrivia previous, SyntaxTrivia next)
    {
        if (previous.Token != next.Token) return false;
        return previous.Token.TrailingTrivia.Contains(previous) == next.Token.TrailingTrivia.Contains(next);
    }

    static CommentKind KindOf(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
            ? CommentKind.Xml
            : CommentKind.Line;

    static CommentBlock Build(string filePath, SyntaxNode root, List<SyntaxTrivia> group)
    {
        var tree = root.SyntaxTree;
        var start = tree.GetLineSpan(group[0].Span).StartLinePosition.Line + 1;
        var end = tree.GetLineSpan(group[^1].Span).EndLinePosition.Line + 1;
        var owner = root.FindNode(group[0].Span, findInsideTrivia: false, getInnermostNodeForTie: true);
        var method = owner.AncestorsAndSelf().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault();
        var body = MethodBodySyntax.BodyOf(method);

        return new CommentBlock(
            FilePath: filePath,
            StartLine: start,
            EndLine: end,
            Text: string.Join("\n", group.Select(Strip)).Trim(),
            Kind: KindOf(group[0]),
            EnclosingMethod: MethodNameOf(method),
            IsInsideMethodBody: body is not null && body.Span.Contains(group[0].SpanStart));
    }

    static string? MethodNameOf(BaseMethodDeclarationSyntax? method) => method switch
    {
        MethodDeclarationSyntax m => m.Identifier.Text,
        ConstructorDeclarationSyntax c => c.Identifier.Text,
        null => null,
        _ => "<operator>",
    };

    static string Strip(SyntaxTrivia trivia)
    {
        var text = trivia.ToFullString().Trim();
        if (text.StartsWith("///")) return text[3..].Trim();
        if (text.StartsWith("//")) return text[2..].Trim();
        if (text.StartsWith("/*")) return text.Trim('/', '*').Trim();
        return text;
    }
}
