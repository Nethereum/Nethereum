using CodeQuality.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuality.Core.Parsing;

public static class SourceFileParser
{
    // Convenience overloads: parse with default options, then delegate. Callers analysing a whole
    // package use the root-based overloads below instead, so each file is parsed exactly once with
    // the configured preprocessor symbols (ruling P16) rather than once per file per parser call.
    public static IReadOnlyList<MethodMetrics> ParseMethods(string filePath, string sourceText) =>
        ParseMethods(filePath, CSharpSyntaxTree.ParseText(sourceText).GetRoot());

    public static IReadOnlyList<CommentBlock> ParseComments(string filePath, string sourceText) =>
        ParseComments(filePath, CSharpSyntaxTree.ParseText(sourceText).GetRoot());

    public static IReadOnlyList<MethodMetrics> ParseMethods(string filePath, SyntaxNode root)
    {
        var methods = root.DescendantNodes()
            .OfType<BaseMethodDeclarationSyntax>()
            .Select(m => Describe(filePath, m));

        var accessors = root.DescendantNodes()
            .OfType<AccessorDeclarationSyntax>()
            .Where(a => a.Body is not null || a.ExpressionBody is not null)
            .Select(a => DescribeAccessor(filePath, a));

        return methods.Concat(accessors).ToList();
    }

    public static IReadOnlyList<CommentBlock> ParseComments(string filePath, SyntaxNode root) =>
        CommentBlockExtractor.Extract(filePath, root);

    static MethodMetrics Describe(string filePath, BaseMethodDeclarationSyntax method)
    {
        var span = method.SyntaxTree.GetLineSpan(method.Span);
        var body = MethodBodySyntax.BodyOf(method);

        return new MethodMetrics(
            FilePath: filePath,
            Line: span.StartLinePosition.Line + 1,
            ContainingType: EnclosingTypeName(method),
            Name: MethodName(method),
            Signature: SignatureOf(method),
            LineCount: span.EndLinePosition.Line - span.StartLinePosition.Line + 1,
            StatementCount: body is null ? 0 : MethodWalker.StatementCount(body),
            MaxNesting: body is null ? 0 : MethodWalker.MaxNesting(body),
            ParameterCount: method.ParameterList.Parameters.Count,
            IsPublic: method.Modifiers.Any(SyntaxKind.PublicKeyword));
    }

    static MethodMetrics DescribeAccessor(string filePath, AccessorDeclarationSyntax accessor)
    {
        var span = accessor.SyntaxTree.GetLineSpan(accessor.Span);
        var body = MethodBodySyntax.BodyOf(accessor);
        var member = accessor.Ancestors().OfType<BasePropertyDeclarationSyntax>().First();
        var name = $"{MemberName(member)}.{accessor.Keyword.Text}";

        return new MethodMetrics(
            FilePath: filePath,
            Line: span.StartLinePosition.Line + 1,
            ContainingType: EnclosingTypeName(accessor),
            Name: name,
            Signature: $"{name}()",
            LineCount: span.EndLinePosition.Line - span.StartLinePosition.Line + 1,
            StatementCount: body is null ? 0 : MethodWalker.StatementCount(body),
            MaxNesting: body is null ? 0 : MethodWalker.MaxNesting(body),
            ParameterCount: 0,
            IsPublic: AccessorIsPublic(accessor, member));
    }

    static bool AccessorIsPublic(AccessorDeclarationSyntax accessor, BasePropertyDeclarationSyntax member) =>
        accessor.Modifiers.Count > 0
            ? accessor.Modifiers.Any(SyntaxKind.PublicKeyword)
            : member.Modifiers.Any(SyntaxKind.PublicKeyword);

    static string MemberName(BasePropertyDeclarationSyntax member) => member switch
    {
        PropertyDeclarationSyntax p => p.Identifier.Text,
        IndexerDeclarationSyntax => "this",
        EventDeclarationSyntax e => e.Identifier.Text,
        _ => "<unknown>",
    };

    static string MethodName(BaseMethodDeclarationSyntax method) => method switch
    {
        MethodDeclarationSyntax m => m.Identifier.Text,
        ConstructorDeclarationSyntax c => c.Identifier.Text,
        DestructorDeclarationSyntax d => "~" + d.Identifier.Text,
        OperatorDeclarationSyntax o => "operator " + o.OperatorToken.Text,
        ConversionOperatorDeclarationSyntax c => "operator " + c.Type.ToString(),
        _ => "<unknown>",
    };

    static string EnclosingTypeName(SyntaxNode node)
    {
        var type = node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
        return type?.Identifier.Text ?? "<global>";
    }

    static string SignatureOf(BaseMethodDeclarationSyntax method)
    {
        var parameters = method.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "?");
        return $"{MethodName(method)}({string.Join(", ", parameters)})";
    }
}
