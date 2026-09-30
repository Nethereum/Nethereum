using System.Text.Json;
using System.Text.Json.Serialization;
using CodeQuality.Core.Analysis;
using CodeQuality.Core.Model;
using CodeQuality.Core.Rules;

namespace CodeQuality.Cli;

public static class JsonRenderer
{
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Summary(AnalysisResult result) => JsonSerializer.Serialize(new
    {
        result.Package,
        result.ProfileName,
        result.ConfigSourcePath,
        result.ReferenceChecksInert,
        result.FilesAnalysed,
        result.FilesExcluded,
        result.FilesUnreadable,
        result.FilesWithDisabledRegions,
        result.MethodCount,
        result.CommentBlockCount,
        result.TypeCount,
        Verdicts = result.Findings.GroupBy(f => f.Verdict)
            .ToDictionary(g => g.Key.ToString(), g => g.Count()),
        Findings = result.Findings.Select(f => Describe(f, result)),
    }, Options);

    // Every non-summary command shares this envelope so an agent reading, say, `comments --json`
    // in isolation can still see the same coverage counts `summary --json` carries (files
    // unreadable, files with unread `#if` regions, which config was used) rather than a bare
    // array that looks identical whether the run read everything or silently skipped most of it.
    public static string Findings(AnalysisResult result, IEnumerable<Finding> findings) =>
        JsonSerializer.Serialize(new
        {
            Coverage = Envelope(result),
            Findings = findings.Select(f => Describe(f, result)),
        }, Options);

    public static string Methods(AnalysisResult result, IEnumerable<MethodMetrics> methods) =>
        JsonSerializer.Serialize(new
        {
            Coverage = Envelope(result),
            Methods = methods,
        }, Options);

    static object Envelope(AnalysisResult result) => new
    {
        result.Package,
        result.ProfileName,
        result.ConfigSourcePath,
        result.ReferenceChecksInert,
        result.FilesAnalysed,
        result.FilesExcluded,
        result.FilesUnreadable,
        result.FilesWithDisabledRegions,
        result.MethodCount,
        result.CommentBlockCount,
        result.TypeCount,
    };

    static object Describe(Finding f) => new
    {
        f.Id, f.Package, f.FilePath, f.Line, f.Symbol,
        Kind = f.Kind.ToString(), f.RuleId, Verdict = f.Verdict.ToString(),
        f.Evidence, f.Confidence, f.Profile,
    };

    // A class-size finding is the one finding whose subject is not at a single file:line, so the
    // flat shape above cannot express it: an agent reading a bare `FilePath` for a type declared
    // across three `partial` files would be told the type lives in one place, which is exactly
    // the misreading the aggregate exists to prevent. The measured type travels WITH the finding
    // rather than in a separate top-level array, so a finding stays self-describing when it is
    // read in isolation.
    static object Describe(Finding f, AnalysisResult result)
    {
        if (f.RuleId != GodClassRule.RuleId) return Describe(f);

        var type = result.Types.FirstOrDefault(t => t.FullName == f.Symbol);
        if (type is null) return Describe(f);

        return new
        {
            f.Id, f.Package, f.FilePath, f.Line, f.Symbol,
            Kind = f.Kind.ToString(), f.RuleId, Verdict = f.Verdict.ToString(),
            f.Evidence, f.Confidence, f.Profile,
            Type = DescribeType(type),
        };
    }

    static object DescribeType(TypeMetrics type) => new
    {
        type.FullName, type.Name, type.Namespace, type.Kind, type.IsPartial,
        type.PrimaryFilePath, type.PrimaryLine,
        type.TotalLines, type.MemberCount, type.MethodCount, type.PropertyCount,
        type.FieldCount, type.EventCount, type.ConstructorCount,
        type.LongMethodCount, type.MaxMethodLinesApplied,
        type.DeclaringFileCount,
        Parts = type.Parts.Select(p => new
        {
            p.FilePath, p.Line, p.LineCount, p.MemberCount, p.MethodCount, p.PropertyCount,
            p.FieldCount, p.EventCount, p.ConstructorCount, p.LongMethodCount, p.IsPartial,
        }),
    };
}
