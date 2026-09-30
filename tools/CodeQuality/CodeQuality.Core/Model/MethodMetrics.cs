namespace CodeQuality.Core.Model;

public sealed record MethodMetrics(
    string FilePath,
    int Line,
    string ContainingType,
    string Name,
    string Signature,
    int LineCount,
    int StatementCount,
    int MaxNesting,
    int ParameterCount,
    bool IsPublic);
