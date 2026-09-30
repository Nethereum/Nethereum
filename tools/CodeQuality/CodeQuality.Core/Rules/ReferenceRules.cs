using System.Text.RegularExpressions;
using CodeQuality.Core.Config;
using CodeQuality.Core.Model;

namespace CodeQuality.Core.Rules;

public enum ReferenceTier { None, SourcePointer, InternalSymbol, Behavioural }

public static class ReferenceRules
{
    public static ReferenceTier Classify(CodeQualityConfig config, string text)
    {
        var rules = config.References;
        var vendors = NonEmptyVendors(rules.Vendors);

        if (vendors.Count == 0) return ReferenceTier.None;
        if (!vendors.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase)))
            return ReferenceTier.None;
        if (Matches(rules.CitationPatterns, text, RegexOptions.IgnoreCase)) return ReferenceTier.None;
        if (Matches(rules.SourcePointerPatterns, text, RegexOptions.IgnoreCase)) return ReferenceTier.SourcePointer;
        // Case-sensitive: camelCase detection depends on the interplay of upper and lower case, so
        // matching this pattern with IgnoreCase (as the other two tiers correctly do) collapses
        // `[a-z]+[A-Z]` to "any two letters" and flags ordinary prose as an internal symbol.
        if (Matches(rules.InternalSymbolPatterns, StripVendors(vendors, text), RegexOptions.None))
            return ReferenceTier.InternalSymbol;

        return ReferenceTier.Behavioural;
    }

    public static IEnumerable<Finding> Apply(RuleContext context, CommentBlock block)
    {
        var tier = Classify(context.Config, block.Text);

        var (ruleId, verdict) = tier switch
        {
            ReferenceTier.SourcePointer => ("reference.source-pointer", Verdict.Rephrase),
            ReferenceTier.InternalSymbol => ("reference.internal-symbol", Verdict.Review),
            ReferenceTier.Behavioural when context.Profile.ReferencesAlwaysDefect
                => ("reference.vendor-mention", Verdict.Rephrase),
            _ => (null, Verdict.Keep),
        };

        if (ruleId is null) yield break;

        yield return new Finding(
            Package: context.Package,
            FilePath: context.RelativePath,
            Line: block.StartLine,
            Symbol: block.EnclosingMethod ?? "<file>",
            Kind: FindingKind.Reference,
            RuleId: ruleId,
            Verdict: verdict,
            Evidence: EvidenceFor(context.Config, tier, block.Text),
            Confidence: 1.0,
            Profile: context.ProfileName);
    }

    // A reference finding is reviewed by reading the Evidence column alone; if it doesn't contain
    // the fragment that made the rule fire, the reviewer is judging prose, not the defect. So
    // evidence is centred on the actual regex/vendor match rather than on the head of the block.
    static string EvidenceFor(CodeQualityConfig config, ReferenceTier tier, string text)
    {
        var normalized = Normalize(text);
        var span = FindTriggerSpan(config, tier, normalized);
        return span is { } s ? CentredOn(normalized, s.Start, s.Length) : Cap(normalized);
    }

    static (int Start, int Length)? FindTriggerSpan(CodeQualityConfig config, ReferenceTier tier, string normalizedText)
    {
        var rules = config.References;
        return tier switch
        {
            ReferenceTier.SourcePointer => MatchSpan(rules.SourcePointerPatterns, normalizedText, RegexOptions.IgnoreCase),
            ReferenceTier.InternalSymbol => InternalSymbolSpan(rules, normalizedText),
            ReferenceTier.Behavioural => VendorSpan(NonEmptyVendors(rules.Vendors), normalizedText),
            _ => null,
        };
    }

    // InternalSymbolPatterns match against vendor-stripped text (see Classify), so the match's
    // position there does not line up with the unstripped text an evidence excerpt is built from.
    // The matched substring itself is never part of a vendor name (stripping would have consumed
    // it), so it can be relocated in the original text by value instead of by offset.
    static (int, int)? InternalSymbolSpan(Config.ReferenceRules rules, string normalizedText)
    {
        var stripped = StripVendors(NonEmptyVendors(rules.Vendors), normalizedText);
        var match = FirstMatch(rules.InternalSymbolPatterns, stripped, RegexOptions.None);
        if (match is null) return null;

        var index = normalizedText.IndexOf(match.Value, StringComparison.Ordinal);
        return index < 0 ? null : (index, match.Value.Length);
    }

    static (int, int)? VendorSpan(IReadOnlyList<string> vendors, string normalizedText)
    {
        foreach (var vendor in vendors)
        {
            var index = normalizedText.IndexOf(vendor, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) return (index, vendor.Length);
        }
        return null;
    }

    static (int, int)? MatchSpan(IEnumerable<string> patterns, string text, RegexOptions options)
    {
        var match = FirstMatch(patterns, text, options);
        return match is null ? null : (match.Index, match.Length);
    }

    static Match? FirstMatch(IEnumerable<string> patterns, string text, RegexOptions options)
    {
        foreach (var pattern in patterns)
        {
            var match = SafeMatch(pattern, text, options);
            if (match is { Success: true }) return match;
        }
        return null;
    }

    static Match? SafeMatch(string pattern, string text, RegexOptions options)
    {
        try
        {
            return Regex.Match(text, pattern, options);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // An empty-string vendor entry satisfies `text.Contains("")` for any text at all, turning the
    // vendor gate into a match-everything gate; a whitespace-only entry is the same defect in
    // practice. Both are filtered here rather than in ConfigLoader, which only normalises null
    // collections, not the values inside them.
    static List<string> NonEmptyVendors(IEnumerable<string> vendors) =>
        vendors.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

    static string StripVendors(IEnumerable<string> vendors, string text) =>
        vendors.Aggregate(text, (acc, v) => Regex.Replace(acc, Regex.Escape(v), " ", RegexOptions.IgnoreCase));

    // A pattern is user-supplied YAML config, so a typo (an unbalanced `[` or `(`) must cost that
    // one pattern, not the run: RegexParseException derives from ArgumentException, and a pattern
    // that fails to compile is treated as one that never matches.
    static bool Matches(IEnumerable<string> patterns, string text, RegexOptions options) =>
        patterns.Any(p => SafeIsMatch(p, text, options));

    static bool SafeIsMatch(string pattern, string text, RegexOptions options)
    {
        try
        {
            return Regex.IsMatch(text, pattern, options);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // Used as-is by CommentedOutCodeRule (the whole point is the code shape, which starts at the
    // head) and TaggedCommentRule (the tag token is conventionally the first word). Neither has a
    // trigger that can be buried deep in a long block the way a source-pointer citation can.
    internal static string Excerpt(string text) => Cap(Normalize(text));

    const int MaxExcerptLength = 120;
    const int TriggerContext = 40;

    static string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    static string Cap(string normalized) =>
        normalized.Length <= MaxExcerptLength ? normalized : normalized[..(MaxExcerptLength - 3)] + "...";

    static string CentredOn(string normalized, int start, int length)
    {
        if (normalized.Length <= MaxExcerptLength) return normalized;

        var end = start + length;
        var windowStart = Math.Max(0, start - TriggerContext);
        var windowEnd = Math.Min(normalized.Length, end + TriggerContext);

        var excerpt = normalized[windowStart..windowEnd];
        if (windowStart > 0) excerpt = "..." + excerpt;
        if (windowEnd < normalized.Length) excerpt += "...";

        return Cap(excerpt);
    }
}
