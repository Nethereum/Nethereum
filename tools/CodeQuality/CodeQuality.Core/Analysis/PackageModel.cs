using CodeQuality.Core.Model;
using Microsoft.CodeAnalysis;

namespace CodeQuality.Core.Analysis;

public sealed record SourceFile(
    string RelativePath,
    string Text,
    SyntaxNode Root,
    IReadOnlyList<MethodMetrics> Methods,
    IReadOnlyList<TypeDeclarationMetrics> TypeParts,
    IReadOnlyList<CommentBlock> Comments);

public sealed record PackageModel(
    string Package,
    string RootPath,
    IReadOnlyList<SourceFile> Files,
    // Types are a PACKAGE-level list, not a per-file one: a `partial` type declared across three
    // files is one entry here. Hanging them off SourceFile would make the merge impossible to
    // express and would score a half-split god class as three compliant classes.
    IReadOnlyList<TypeMetrics> Types,
    int FilesExcluded,
    int FilesUnreadable,
    int FilesWithDisabledRegions)
{
    public IEnumerable<MethodMetrics> AllMethods => Files.SelectMany(f => f.Methods);
    public IEnumerable<CommentBlock> AllComments => Files.SelectMany(f => f.Comments);
}
