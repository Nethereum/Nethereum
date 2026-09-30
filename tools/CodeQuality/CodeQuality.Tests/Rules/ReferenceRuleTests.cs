using CodeQuality.Core.Config;
using CodeQuality.Core.Model;
using CodeQuality.Core.Rules;
using Xunit;
// CodeQuality.Core.Config and CodeQuality.Core.Rules both declare a type named `ReferenceRules`
// (the config section and the rule class); importing both makes the bare name ambiguous. This
// alias resolves it to the rule class, which is the only one this file references by that name.
using ReferenceRules = CodeQuality.Core.Rules.ReferenceRules;

namespace CodeQuality.Tests.Rules;

public class ReferenceRuleTests
{
    const string Yaml = """
        references:
          vendors: ["acme"]
          sourcePointerPatterns: ["\\.(go|py|rs)\\b", ":\\d+", "\\b\\d{3,5}\\s*-\\s*\\d{3,5}\\b"]
          internalSymbolPatterns: ["\\b[a-z]+[A-Z][A-Za-z]*\\b"]
          citationPatterns: ["SPEC-\\d+"]
        profiles:
          lenient:
            maxMethodLines: 35
          strict:
            maxMethodLines: 35
            referencesAlwaysDefect: true
        """;

    static readonly CodeQualityConfig Config = ConfigLoader.LoadFrom(Yaml);

    static RuleContext Context(string profileName) => new(
        Package: "Pkg", RelativePath: "src/Pkg/Thing.cs",
        ProfileName: profileName, Profile: Config.Profiles[profileName], Config: Config);

    static CommentBlock Block(string text) =>
        new("src/Pkg/Thing.cs", 10, 10, text, CommentKind.Line, "Do", true);

    [Theory]
    [InlineData("acme (net/protocol/handlers.go ServiceQuery, case 0)", ReferenceTier.SourcePointer)]
    [InlineData("ports acme's chunk count (sync.go:2118-2144)", ReferenceTier.SourcePointer)]
    [InlineData("acme onStorage cross-references each account", ReferenceTier.InternalSymbol)]
    [InlineData("acme rejects an over-length response outright", ReferenceTier.Behavioural)]
    [InlineData("a peer that fails transiently becomes dialable", ReferenceTier.None)]
    public void ClassifiesReferenceTier(string text, ReferenceTier expected) =>
        Assert.Equal(expected, ReferenceRules.Classify(Config, text));

    [Fact]
    public void SourcePointerIsAlwaysRephrase()
    {
        var finding = Assert.Single(ReferenceRules.Apply(Context("lenient"), Block("see sync.go:2118 in acme")));

        Assert.Equal("reference.source-pointer", finding.RuleId);
        Assert.Equal(Verdict.Rephrase, finding.Verdict);
        Assert.Equal(FindingKind.Reference, finding.Kind);
    }

    [Fact]
    public void BehaviouralReferenceIsKeptUnderALenientProfile() =>
        Assert.Empty(ReferenceRules.Apply(Context("lenient"), Block("acme rejects an over-length response")));

    [Fact]
    public void BehaviouralReferenceIsADefectUnderAStrictProfile()
    {
        var finding = Assert.Single(ReferenceRules.Apply(Context("strict"), Block("acme rejects an over-length response")));
        Assert.Equal(Verdict.Rephrase, finding.Verdict);
    }

    [Fact]
    public void InternalSymbolIsFlaggedForReviewNotAction()
    {
        var finding = Assert.Single(ReferenceRules.Apply(Context("lenient"), Block("acme onStorage verifies each account")));

        Assert.Equal("reference.internal-symbol", finding.RuleId);
        Assert.Equal(Verdict.Review, finding.Verdict);
    }

    [Fact]
    public void NoVendorConfiguredMeansNoReferenceFindings()
    {
        var bare = ConfigLoader.LoadFrom(string.Empty);
        var context = new RuleContext("Pkg", "src/Pkg/Thing.cs", "default", bare.Profiles["default"], bare);

        Assert.Empty(ReferenceRules.Apply(context, Block("see sync.go:2118 in acme")));
    }

    [Fact]
    public void CitationPatternIsNeverFlagged() =>
        Assert.Empty(ReferenceRules.Apply(Context("strict"), Block("SPEC-4844 requires the blob base fee to update")));

    [Fact]
    public void CommentedOutCodeIsDelete()
    {
        var finding = Assert.Single(CommentedOutCodeRule.Apply(Context("lenient"), Block("var x = Compute(y);")));

        Assert.Equal("comment.commented-out-code", finding.RuleId);
        Assert.Equal(Verdict.Delete, finding.Verdict);
    }

    [Theory]
    [InlineData("the peer rotates on a byzantine response.")]
    [InlineData("Retry for the peer (see above) before giving up")]
    [InlineData("If the difference exceeds the gap we stop (this is deliberate)")]
    [InlineData("returns early when the queue is empty (no work to do)")]
    public void ProseIsNotMistakenForCommentedOutCode(string prose) =>
        Assert.Empty(CommentedOutCodeRule.Apply(Context("lenient"), Block(prose)));

    // Carried over from round 2's removed `DeleteNeverFiresWithoutCodeShapedEvidence`: a line that
    // starts with `{` but is not brace-only must not be mistaken for the brace-only special case,
    // and since it neither parses as code nor ends in `;`, it produces no finding at all (unlike
    // the guard-skip and semicolon-only paths, which must always surface as `Review`).
    [Fact]
    public void BraceAtStartButNotBraceOnlyProducesNoFinding() =>
        Assert.Empty(CommentedOutCodeRule.Apply(Context("lenient"), Block("{ not actually just a brace")));

    [Fact]
    public void TaggedCommentIsReview()
    {
        var finding = Assert.Single(TaggedCommentRule.Apply(Context("lenient"), Block("TODO: handle the empty case")));

        Assert.Equal("comment.tagged", finding.RuleId);
        Assert.Equal(Verdict.Review, finding.Verdict);
    }

    // --- Hostile-input coverage (Task 8 standing instructions) ---

    [Fact]
    public void EmptyCommentTextProducesNoFindingsAndDoesNotThrow()
    {
        Assert.Empty(ReferenceRules.Apply(Context("strict"), Block(string.Empty)));
        Assert.Empty(CommentedOutCodeRule.Apply(Context("lenient"), Block(string.Empty)));
        Assert.Empty(TaggedCommentRule.Apply(Context("lenient"), Block(string.Empty)));
    }

    [Fact]
    public void CommentWithNullEnclosingMethodUsesFileSymbol()
    {
        var fileLevel = new CommentBlock("src/Pkg/Thing.cs", 3, 3, "see sync.go:2118 in acme", CommentKind.Line, null, false);

        var finding = Assert.Single(ReferenceRules.Apply(Context("lenient"), fileLevel));

        Assert.Equal("<file>", finding.Symbol);
    }

    // An empty-string vendor entry makes `text.Contains("")` true for any text, which — absent a
    // guard — turns the vendor gate into a match-everything gate. This is proven with a throwaway
    // probe (see task-8-report.md); the fix filters blank vendor entries before use.
    [Fact]
    public void EmptyStringVendorDoesNotMatchEveryComment()
    {
        var yaml = """
            references:
              vendors: [""]
              sourcePointerPatterns: [":\\d+"]
            profiles:
              lenient:
                maxMethodLines: 35
            """;
        var config = ConfigLoader.LoadFrom(yaml);
        var context = new RuleContext("Pkg", "src/Pkg/Thing.cs", "lenient", config.Profiles["lenient"], config);

        Assert.Equal(ReferenceTier.None, ReferenceRules.Classify(config, "a peer that fails transiently becomes dialable"));
        Assert.Empty(ReferenceRules.Apply(context, Block("a peer that fails transiently becomes dialable")));
    }

    // A malformed pattern (unbalanced `[`) is user-supplied YAML, not attacker input, but a typo
    // must degrade that one pattern rather than crash the whole run. Proven via probe: an
    // unguarded Regex.IsMatch on `"[a-"` throws RegexParseException (an ArgumentException).
    [Fact]
    public void InvalidRegexPatternInConfigDoesNotThrowAndDegradesToNoMatch()
    {
        var yaml = """
            references:
              vendors: ["acme"]
              sourcePointerPatterns: ["[a-"]
            profiles:
              lenient:
                maxMethodLines: 35
            """;
        var config = ConfigLoader.LoadFrom(yaml);

        var tier = Record.Exception(() => ReferenceRules.Classify(config, "acme rejects an over-length response"));

        Assert.Null(tier);
        Assert.Equal(ReferenceTier.Behavioural, ReferenceRules.Classify(config, "acme rejects an over-length response"));
    }

    [Fact]
    public void VeryLongCommentTextDoesNotThrow()
    {
        var longText = "acme rejects an over-length response outright. " + new string('x', 20_000);

        var ex = Record.Exception(() => ReferenceRules.Apply(Context("lenient"), Block(longText)).ToList());

        Assert.Null(ex);
    }

    // --- Task 14: evidence must show why the rule fired, not just the head of the block ---
    // A reviewer judges a finding by reading the Evidence column alone. If the trigger that
    // classified the block is buried past the old 120-character head-only excerpt, the reviewer
    // sees only prose and cannot tell the finding is correct. These four cases were measured
    // failing against the pre-fix `Excerpt(block.Text)` evidence before the fix landed.

    [Fact]
    public void EvidenceForATriggerNearTheEndOfALongBlockIncludesTheTrigger()
    {
        var filler = string.Join(" ", Enumerable.Repeat(
            "the walker only ever advances past a checkpoint once every branch below it has verified cleanly",
            6));
        var text = "acme " + filler + " see sync.go:2573-2578 for the original derivation";

        var finding = Assert.Single(ReferenceRules.Apply(Context("lenient"), Block(text)));

        Assert.Equal("reference.source-pointer", finding.RuleId);
        // Hand-written literal: the exact substring the sourcePointerPatterns regex matches here.
        Assert.Contains("sync.go:2573-2578", finding.Evidence);
        Assert.True(finding.Evidence.Length <= 120);
        // The trigger sits ~600 characters into the block, far past the 40-character context
        // radius, so the leading context must have been elided.
        Assert.StartsWith("...", finding.Evidence);
    }

    // The regex that actually fires here is `\.(go|py|rs)\b`, matching just ".go" at index 9 (not
    // the whole "sync.go:2118" citation) — the window is built around that 3-character match. The
    // filler is a fixed run of 'z' so the exact expected window can be hand-counted rather than
    // taken from running the rule: 40-char radius clamped to 0 at the front (nothing to elide,
    // matchIndex 9 < 40) and index 12+40=52 at the back, i.e. characters [0,52) of the 18-character
    // prefix ("acme sync.go:2118 ") plus 34 'z's, then a trailing ellipsis. The old head-only
    // `Excerpt` instead always cuts at a fixed 117 characters regardless of the match, producing a
    // 99-'z' string — a different literal — so this fails against the pre-fix code even though both
    // happen to start the same way.
    [Fact]
    public void EvidenceForATriggerAtTheStartOfALongBlockHasNoLeadingEllipsis()
    {
        var text = "acme sync.go:2118 " + new string('z', 200);
        var expected = "acme sync.go:2118 " + new string('z', 34) + "...";

        var finding = Assert.Single(ReferenceRules.Apply(Context("lenient"), Block(text)));

        Assert.Equal(expected, finding.Evidence);
        Assert.False(finding.Evidence.StartsWith("..."));
    }

    [Fact]
    public void EvidenceForAShortBlockIsTheWholeBlockUnchanged()
    {
        const string text = "see sync.go:12 in acme code";

        var finding = Assert.Single(ReferenceRules.Apply(Context("lenient"), Block(text)));

        // The block already fits under the cap, so evidence must be exactly the (whitespace-
        // normalised) block, with no ellipses added anywhere.
        Assert.Equal(text, finding.Evidence);
    }

    [Fact]
    public void EvidenceNeverExceedsTheLengthCapEvenForAVeryLongBlockWithAHugeTrigger()
    {
        var text = "acme " + new string('x', 5_000) + " sync.go:2118 " + new string('y', 5_000);

        var finding = Assert.Single(ReferenceRules.Apply(Context("lenient"), Block(text)));

        Assert.True(finding.Evidence.Length <= 120);
        Assert.Contains("sync.go:2118", finding.Evidence);
    }

    // --- Round 3: ask the parser, not a regex ---
    // Rounds 1 and 2 both tried to detect "is this code?" with punctuation heuristics over a single
    // line and both produced false `Delete`s on real prose (round 1: any trailing `;`; round 2: `;`
    // plus `(` or `=`, defeated by a parenthetical aside, an equality statement, or a URL query
    // string). Ground truth below is hand-assigned by reading each input, independently of the
    // parser-based implementation, per the round-3 ruling's Second Finding: a test that re-derives
    // its expected result from the same predicate the rule uses can only confirm the code agrees
    // with itself, never that the logic is right. If a future change flips any row, this must fail.
    public static IEnumerable<object[]> HandLabelledGroundTruth()
    {
        // MUST be Delete: unambiguously executable C#.
        yield return new object[] { "registry.Register<Foo, Bar>();", "comment.commented-out-code", Verdict.Delete };
        yield return new object[] { "var x = Compute(y);", "comment.commented-out-code", Verdict.Delete };
        yield return new object[] { "var x = 1;", "comment.commented-out-code", Verdict.Delete };
        // Recovers the true positive round 2 knowingly gave up: no `(`, no `=`, but genuinely code.
        yield return new object[] { "using System.Dynamic;", "comment.commented-out-code", Verdict.Delete };
        yield return new object[] { "if (x > 0) { return; }", "comment.commented-out-code", Verdict.Delete };
        yield return new object[] { "{", "comment.commented-out-code", Verdict.Delete };
        yield return new object[] { "}", "comment.commented-out-code", Verdict.Delete };

        // MUST NOT be Delete: the six confirmed false positives from the round-2 gate, verbatim.
        yield return new object[] { "Retry (see above); this is deliberate;", "comment.possible-commented-out-code", Verdict.Review };
        yield return new object[] { "Here x = y by construction;", "comment.possible-commented-out-code", Verdict.Review };
        yield return new object[] { "This handles the base case (n == 0);", "comment.possible-commented-out-code", Verdict.Review };
        yield return new object[] { "The invariant holds because count = total always;", "comment.possible-commented-out-code", Verdict.Review };
        yield return new object[] { "see http://example.com/path?x=1;", "comment.possible-commented-out-code", Verdict.Review };
        yield return new object[] { "The call site does `Foo(bar);` internally, which is fine;", "comment.possible-commented-out-code", Verdict.Review };

        // MUST NOT be Delete: syntactically legal but not code (an identifier is not a statement).
        yield return new object[] { "done;", "comment.possible-commented-out-code", Verdict.Review };
        yield return new object[] { "Note;", "comment.possible-commented-out-code", Verdict.Review };

        // MUST NOT be Delete: real `;`-terminated comments from src/, no `(`, no `=`.
        yield return new object[] { "getHash is computed BEFORE the op is signed and excludes paymasterAndData/signature;", "comment.possible-commented-out-code", Verdict.Review };
        yield return new object[] { "ERC-4337: the four paymaster fields must be present together or absent together;", "comment.possible-commented-out-code", Verdict.Review };
        yield return new object[] { "The tx mapping only exists to revert a submitted-but-dropped bundle;", "comment.possible-commented-out-code", Verdict.Review };
        yield return new object[] { "A common divisor cannot be greater than right;", "comment.possible-commented-out-code", Verdict.Review };
        yield return new object[] { "The bundler talks to the chain through IWeb3, not an in-process IChainNode;", "comment.possible-commented-out-code", Verdict.Review };

        // Carried over from round 2's removed `DeleteNeverFiresWithoutCodeShapedEvidence`, so no
        // literal string that test exercised is left unaccounted for by this table (see the round-3
        // report's per-row justification for the removal). Each verdict below was independently
        // verified against the implementation before being written here, not assumed.
        yield return new object[] { "  }  ", "comment.commented-out-code", Verdict.Delete };
        yield return new object[] { "Foo();", "comment.commented-out-code", Verdict.Delete };
        yield return new object[] { "handles three outcomes: success, retry, escalate;", "comment.possible-commented-out-code", Verdict.Review };
        // Round 2 evaluated evidence per line, so a valid call on one line of a multi-line block
        // could carry an unrelated prose line to `Delete`. Round 3 parses the whole block as one
        // unit, so the invalid prose line poisons the parse regardless of position — a stricter,
        // safer outcome than round 2's, and one round 2 could not have produced.
        yield return new object[] { "first line is prose;\nsecond line calls Foo();", "comment.possible-commented-out-code", Verdict.Review };
        yield return new object[] { "first line calls Foo();\nsecond line is prose;", "comment.possible-commented-out-code", Verdict.Review };
    }

    [Theory]
    [MemberData(nameof(HandLabelledGroundTruth))]
    public void CommentedOutCodeVerdictMatchesHandLabelledGroundTruth(string text, string expectedRuleId, Verdict expectedVerdict)
    {
        var finding = Assert.Single(CommentedOutCodeRule.Apply(Context("lenient"), Block(text)));

        Assert.Equal(expectedRuleId, finding.RuleId);
        Assert.Equal(expectedVerdict, finding.Verdict);
    }

    // A pathologically deep nesting (`(((((...`) drives Roslyn's recursive-descent parser into an
    // uncatchable stack overflow that kills the process outright — confirmed by probe: depth 1000
    // parses cleanly, depth 1500 crashes the process. If the depth guard in the rule is ever
    // removed or weakened, this test does not fail red — it takes the whole test run down with it,
    // which is itself the proof the guard matters. It must still surface as `Review`, not silence,
    // and its evidence must say plainly that the text was not parsed and why.
    [Fact]
    public void PathologicallyNestedCommentDoesNotCrashTheProcessAndSurfacesAsReview()
    {
        var deeplyNested = new string('(', 5000) + ");";

        var finding = Assert.Single(CommentedOutCodeRule.Apply(Context("lenient"), Block(deeplyNested)));

        Assert.Equal("comment.possible-commented-out-code", finding.RuleId);
        Assert.Equal(Verdict.Review, finding.Verdict);
        Assert.Contains("not parsed", finding.Evidence);
        Assert.Contains("deeply nested", finding.Evidence);
    }

    // A moderately-nested but very long comment must not be silently dropped either — it should
    // read the same as any other declined-to-parse input: `Review`, with the length reason named.
    [Fact]
    public void ExcessivelyLongFlatCommentSurfacesAsReviewNamingTheLengthReason()
    {
        var longFlat = "x = 1;" + new string('a', 10_500);

        var finding = Assert.Single(CommentedOutCodeRule.Apply(Context("lenient"), Block(longFlat)));

        Assert.Equal("comment.possible-commented-out-code", finding.RuleId);
        Assert.Equal(Verdict.Review, finding.Verdict);
        Assert.Contains("not parsed", finding.Evidence);
        Assert.Contains("too long", finding.Evidence);
    }

    // The capability the round-3 length cap silently removed: a long-but-shallow commented-out
    // method is exactly the genuine `Delete` case this rule exists to catch, and must not be
    // penalised just because it has many characters. Built programmatically so the length claim is
    // verified, not eyeballed, and kept well under the 10,000-character performance cap while
    // comfortably over the old, wrongly-safety-motivated 500-character one.
    [Fact]
    public void LongButShallowCommentedOutMethodIsStillDelete()
    {
        var statements = Enumerable.Range(0, 40)
            .Select(i => $"accumulator = accumulator + ComputeStep(items[{i}]);");
        var text = "var accumulator = 0;\n" + string.Join("\n", statements) + "\nreturn accumulator;";
        Assert.True(text.Length > 500, "fixture must exceed the old, removed length-based safety cap");
        Assert.True(text.Length < 10_000, "fixture must stay under the current performance-only cap");

        var finding = Assert.Single(CommentedOutCodeRule.Apply(Context("lenient"), Block(text)));

        Assert.Equal("comment.commented-out-code", finding.RuleId);
        Assert.Equal(Verdict.Delete, finding.Verdict);
    }
}
