namespace CodeQuality.Core.Model;

public sealed record CloneGroup(
    string Hash,
    int StatementCount,
    IReadOnlyList<MethodMetrics> Members);
