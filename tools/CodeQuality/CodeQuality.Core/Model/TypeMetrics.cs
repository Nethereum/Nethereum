namespace CodeQuality.Core.Model;

// One `partial` part of a type: what a single FILE declares. A god class that has been split
// into partials produces several of these and exactly one TypeMetrics, which is the whole point
// of keeping the two records apart - see TypeMetrics below.
public sealed record TypeDeclarationMetrics(
    string FilePath,
    int Line,
    int LineCount,
    string FullName,
    string Name,
    string Namespace,
    string Kind,
    bool IsPartial,
    int MethodCount,
    int PropertyCount,
    int FieldCount,
    int EventCount,
    int ConstructorCount,
    int LongMethodCount)
{
    public int MemberCount =>
        MethodCount + PropertyCount + FieldCount + EventCount + ConstructorCount;
}

// A TYPE, aggregated across every file that declares part of it.
//
// Measuring per file instead would be the one mistake that makes this metric useless here: the
// recommended remedy for an oversized type is to split it into `partial` files first and then
// into real classes, so a per-file measure would score the half-finished remedy as a cure while
// the responsibility count is unchanged. Every count below is therefore a SUM over Parts, and
// Parts is kept so a report can show where the mass actually sits.
//
// Counting rules, all chosen so a number can be checked by hand against the source:
//   - Members are the type's OWN direct members. A nested type is its own TypeMetrics; its
//     members are not counted into the outer type.
//   - A field or event declaring several variables in one statement (`int a, b;`) counts once
//     per variable, because that is how many things a reader has to hold.
//   - Indexers count as properties; operators, conversions and destructors count as methods.
//   - LongMethodCount ranges over exactly the same declarations as the per-method
//     structure.long-method rule (every method-like declaration plus every property/event
//     accessor that has a body), so the two numbers can be cross-checked against each other.
//     MaxMethodLinesApplied records the limit it was measured against.
public sealed record TypeMetrics(
    string FullName,
    string Name,
    string Namespace,
    string Kind,
    bool IsPartial,
    string PrimaryFilePath,
    int PrimaryLine,
    int TotalLines,
    int MethodCount,
    int PropertyCount,
    int FieldCount,
    int EventCount,
    int ConstructorCount,
    int LongMethodCount,
    int MaxMethodLinesApplied,
    IReadOnlyList<TypeDeclarationMetrics> Parts)
{
    public int MemberCount =>
        MethodCount + PropertyCount + FieldCount + EventCount + ConstructorCount;

    // Distinct rather than Parts.Count: two partial declarations of one type in a single file is
    // legal C#, and it is one file to open, not two.
    public IReadOnlyList<string> DeclaringFiles =>
        Parts.Select(p => p.FilePath).Distinct(StringComparer.Ordinal)
             .OrderBy(p => p, StringComparer.Ordinal).ToList();

    public int DeclaringFileCount => DeclaringFiles.Count;
}
