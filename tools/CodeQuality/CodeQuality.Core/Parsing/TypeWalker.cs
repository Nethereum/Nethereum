using CodeQuality.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuality.Core.Parsing;

public static class TypeWalker
{
    // Enums are deliberately not collected: `EnumDeclarationSyntax` is a BaseTypeDeclarationSyntax
    // but not a TypeDeclarationSyntax, and a fifty-member enum is a list, not a class that needs
    // decomposing. Classes, structs, records and interfaces are all TypeDeclarationSyntax and all
    // do carry the smell - a thirty-member interface is a god interface.
    // Convenience overload mirroring SourceFileParser: parse with default options, then delegate.
    // A caller analysing a whole package uses the root-based overload so each file is parsed once
    // with the configured preprocessor symbols.
    public static IReadOnlyList<TypeDeclarationMetrics> Collect(
        string filePath, string sourceText, int maxMethodLines) =>
        Collect(filePath, CSharpSyntaxTree.ParseText(sourceText).GetRoot(), maxMethodLines);

    public static IReadOnlyList<TypeDeclarationMetrics> Collect(
        string filePath, SyntaxNode root, int maxMethodLines) =>
        root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Select(type => Describe(filePath, type, maxMethodLines))
            .ToList();

    // The merge key is the fully qualified name INCLUDING generic arity and the chain of
    // enclosing types, so `A.Thing` and `B.Thing` stay two types and `Cache` and `Cache<T>` stay
    // two types. Merging on the simple name would report a false aggregate for either pair.
    public static IReadOnlyList<TypeMetrics> Merge(
        IEnumerable<TypeDeclarationMetrics> declarations, int maxMethodLines) =>
        declarations
            .GroupBy(d => d.FullName, StringComparer.Ordinal)
            .Select(group => Aggregate(group.ToList(), maxMethodLines))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

    static TypeMetrics Aggregate(List<TypeDeclarationMetrics> parts, int maxMethodLines)
    {
        var ordered = parts
            .OrderBy(p => p.FilePath, StringComparer.Ordinal)
            .ThenBy(p => p.Line)
            .ToList();

        // The primary declaration is the LARGEST part: for a type split into partials that is
        // where a reader starts, and it is what a single file:line citation should point at.
        // Ties fall back to the ordinal-first file so the choice is deterministic across runs.
        var primary = ordered
            .OrderByDescending(p => p.LineCount)
            .ThenBy(p => p.FilePath, StringComparer.Ordinal)
            .ThenBy(p => p.Line)
            .First();

        return new TypeMetrics(
            FullName: primary.FullName,
            Name: primary.Name,
            Namespace: primary.Namespace,
            Kind: primary.Kind,
            IsPartial: ordered.Any(p => p.IsPartial),
            PrimaryFilePath: primary.FilePath,
            PrimaryLine: primary.Line,
            TotalLines: ordered.Sum(p => p.LineCount),
            MethodCount: ordered.Sum(p => p.MethodCount),
            PropertyCount: ordered.Sum(p => p.PropertyCount),
            FieldCount: ordered.Sum(p => p.FieldCount),
            EventCount: ordered.Sum(p => p.EventCount),
            ConstructorCount: ordered.Sum(p => p.ConstructorCount),
            LongMethodCount: ordered.Sum(p => p.LongMethodCount),
            MaxMethodLinesApplied: maxMethodLines,
            Parts: ordered);
    }

    static TypeDeclarationMetrics Describe(
        string filePath, TypeDeclarationSyntax type, int maxMethodLines)
    {
        var span = type.SyntaxTree.GetLineSpan(type.Span);

        return new TypeDeclarationMetrics(
            FilePath: filePath,
            Line: span.StartLinePosition.Line + 1,
            LineCount: span.EndLinePosition.Line - span.StartLinePosition.Line + 1,
            FullName: FullNameOf(type),
            Name: type.Identifier.Text,
            Namespace: NamespaceOf(type),
            Kind: type.Keyword.ValueText,
            IsPartial: type.Modifiers.Any(SyntaxKind.PartialKeyword),
            MethodCount: type.Members.Count(m =>
                m is MethodDeclarationSyntax or OperatorDeclarationSyntax
                  or ConversionOperatorDeclarationSyntax or DestructorDeclarationSyntax),
            PropertyCount: type.Members.Count(m =>
                m is PropertyDeclarationSyntax or IndexerDeclarationSyntax),
            FieldCount: type.Members.OfType<FieldDeclarationSyntax>()
                .Sum(f => f.Declaration.Variables.Count),
            EventCount: type.Members.OfType<EventFieldDeclarationSyntax>()
                .Sum(e => e.Declaration.Variables.Count)
                + type.Members.Count(m => m is EventDeclarationSyntax),
            ConstructorCount: type.Members.Count(m => m is ConstructorDeclarationSyntax),
            LongMethodCount: MethodLineCounts(type).Count(lines => lines > maxMethodLines));
    }

    // Mirrors SourceFileParser.ParseMethods exactly - every method-like declaration, plus every
    // accessor that actually has a body (an auto-property's accessors have none and are not
    // methods) - but restricted to this type's OWN members, so a nested type's long methods are
    // charged to the nested type.
    static IEnumerable<int> MethodLineCounts(TypeDeclarationSyntax type)
    {
        foreach (var member in type.Members)
        {
            if (member is BaseMethodDeclarationSyntax method)
            {
                yield return LineCountOf(method);
                continue;
            }

            if (member is not BasePropertyDeclarationSyntax { AccessorList: { } accessors })
                continue;

            foreach (var accessor in accessors.Accessors)
                if (accessor.Body is not null || accessor.ExpressionBody is not null)
                    yield return LineCountOf(accessor);
        }
    }

    static int LineCountOf(SyntaxNode node)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
    }

    static string FullNameOf(TypeDeclarationSyntax type)
    {
        var chain = type.AncestorsAndSelf()
            .OfType<TypeDeclarationSyntax>()
            .Select(NameWithArity)
            .Reverse();

        var qualified = string.Join(".", chain);
        var space = NamespaceOf(type);

        return space.Length == 0 ? qualified : $"{space}.{qualified}";
    }

    static string NameWithArity(TypeDeclarationSyntax type) =>
        type.TypeParameterList is { Parameters.Count: > 0 } parameters
            ? $"{type.Identifier.Text}`{parameters.Parameters.Count}"
            : type.Identifier.Text;

    // Ancestors are innermost-first, so a nested `namespace A { namespace B { ... } }` yields
    // B then A and has to be reversed to read A.B.
    static string NamespaceOf(SyntaxNode node) =>
        string.Join(".", node.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(n => n.Name.ToString())
            .Reverse());
}
