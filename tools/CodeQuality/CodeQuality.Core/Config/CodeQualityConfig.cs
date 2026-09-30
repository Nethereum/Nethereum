namespace CodeQuality.Core.Config;

public sealed class GeneratedFileRules
{
    public List<string> FileNameSuffixes { get; set; } = new();
    public List<string> FileNamePrefixes { get; set; } = new();
    public List<string> PathSegments { get; set; } = new();
    public List<string> HeaderMarkers { get; set; } = new();
    public int HeaderScanChars { get; set; } = 400;
}

public sealed class Profile
{
    public int MaxMethodLines { get; set; } = 35;
    public int MaxNesting { get; set; } = 3;
    public int MaxParameters { get; set; } = 6;

    // Class-size limits, ranking the TYPE (aggregated across all its `partial` files) rather
    // than the method - see GodClassRule for why neither of these is an aggregate line count.
    //
    // 20 members and 3 over-long methods are the figures a measured type-size distribution put
    // between p90 and p95, and at p99, over the reference tree the tool was calibrated against;
    // the config file that ships with that tree states them explicitly, with the distribution,
    // rather than relying on these. A config is expected to do the same rather than inherit
    // numbers whose provenance it cannot show.
    public int MaxTypeMembers { get; set; } = 20;
    public int MaxLongMethodsPerType { get; set; } = 3;
    public bool ReferencesAlwaysDefect { get; set; }
    public Dictionary<string, string> RuleVerdicts { get; set; } = new();
}

public sealed class ProfileBinding
{
    public string Glob { get; set; } = string.Empty;
    public string Profile { get; set; } = "default";
}

public sealed class ReferenceRules
{
    public List<string> Vendors { get; set; } = new();
    public List<string> SourcePointerPatterns { get; set; } = new();
    public List<string> InternalSymbolPatterns { get; set; } = new();
    public List<string> CitationPatterns { get; set; } = new();
}

public sealed class CloneRules
{
    public int MinStatements { get; set; } = 8;
}

// Ruling P16: with no preprocessor symbols defined, every `#if` region parses as disabled text —
// its methods and comments are invisible. The reference repository has 1,350 such directives
// across 425 files, 225 of them in its most safety-critical package. Which symbols are active is
// a property of the codebase, so it is configuration.
public sealed class ParseRules
{
    public List<string> PreprocessorSymbols { get; set; } = new();
}

public sealed class CodeQualityConfig
{
    public GeneratedFileRules Generated { get; set; } = new();
    public Dictionary<string, Profile> Profiles { get; set; } = new();
    public List<ProfileBinding> Bindings { get; set; } = new();
    public ReferenceRules References { get; set; } = new();
    public CloneRules Clones { get; set; } = new();
    public ParseRules Parse { get; set; } = new();
    public string RulesVersion { get; set; } = string.Empty;

    // Null means no ruleset file was found (or none was given) and built-in defaults are in
    // force; set, it is the file the report header must credit so a reader never has to guess
    // which rules actually ran.
    public string? SourcePath { get; set; }
}
