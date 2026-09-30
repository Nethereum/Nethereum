namespace CodeQuality.Core.Model;

public enum Verdict { Keep, Rephrase, Seam, Absorb, Delete, Review }

public enum FindingKind { Structure, Comment, Reference, Clone, Docs }

public sealed record Finding(
    string Package,
    string FilePath,
    int Line,
    string Symbol,
    FindingKind Kind,
    string RuleId,
    Verdict Verdict,
    string Evidence,
    double Confidence,
    string Profile)
{
    public string Id => $"{Package}:{FilePath.Replace('\\', '/')}:{Line}:{Symbol}:{RuleId}";
}
