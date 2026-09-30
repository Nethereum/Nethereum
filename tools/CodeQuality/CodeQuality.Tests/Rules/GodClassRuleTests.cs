using CodeQuality.Core.Config;
using CodeQuality.Core.Model;
using CodeQuality.Core.Parsing;
using CodeQuality.Core.Rules;
using Xunit;

namespace CodeQuality.Tests.Rules;

public class GodClassRuleTests
{
    const int MethodLineLimit = 35;

    static RuleContext Context(Profile? profile = null) => new(
        Package: "Pkg",
        RelativePath: string.Empty,
        ProfileName: "default",
        Profile: profile ?? new Profile(),
        Config: ConfigLoader.LoadFrom(string.Empty));

    static IReadOnlyList<TypeMetrics> Types(params (string File, string Source)[] files) =>
        TypeWalker.Merge(
            files.SelectMany(f => TypeWalker.Collect(f.File, f.Source, MethodLineLimit)),
            MethodLineLimit);

    static IReadOnlyList<Finding> Findings(RuleContext context, IEnumerable<TypeMetrics> types) =>
        types.SelectMany(type =>
            GodClassRule.Apply(context with { RelativePath = type.PrimaryFilePath }, type)).ToList();

    static string Members(int count, int startAt = 0) =>
        string.Join("\n", Enumerable.Range(startAt, count)
            .Select(i => $"    public int Member{i}() => {i};"));

    static string Part(string ns, string name, int members, int startAt = 0) => $$"""
        namespace {{ns}};

        public partial class {{name}}
        {
        {{Members(members, startAt)}}
        }
        """;

    [Fact]
    public void ATypeWithinBothLimitsProducesNoFinding() =>
        Assert.Empty(Findings(Context(), Types(("Small.cs", Part("Sample", "Small", 5)))));

    // THE false green this rule exists to prevent. The recommended remedy for an oversized type
    // in this codebase is to split it into `partial` files first and only then into real classes.
    // A rule that measured per FILE would watch a 24-member type become two 12-member files and
    // report the tree clean, having changed nothing about the type's responsibility count - it
    // would report the half-done remedy as the cure.
    //
    // Both halves of that are asserted here: each file ALONE is below the limit (so a per-file
    // implementation genuinely would find nothing), and the merged type is above it. Asserting
    // only the second half would pass for an implementation that simply summed every type in the
    // package, which is why the per-file measurement is taken and pinned too.
    [Fact]
    public void SplittingAnOversizedTypeAcrossPartialFilesDoesNotSilenceTheFinding()
    {
        var partOne = ("Big.cs", Part("Sample", "Big", 12));
        var partTwo = ("Big.Extra.cs", Part("Sample", "Big", 12, startAt: 12));

        Assert.Empty(Findings(Context(), Types(partOne)));
        Assert.Empty(Findings(Context(), Types(partTwo)));

        var merged = Types(partOne, partTwo);
        var finding = Assert.Single(Findings(Context(), merged));

        Assert.Equal("structure.god-class", finding.RuleId);
        Assert.Equal("Sample.Big", finding.Symbol);
        Assert.Equal(FindingKind.Structure, finding.Kind);
        Assert.Equal(Verdict.Review, finding.Verdict);
        Assert.Contains("24 members (limit 20)", finding.Evidence);
    }

    // Non-vacuity twin for the test above. An implementation that merged on the SIMPLE name
    // would pass it - and would invent a 24-member `Thing` that exists nowhere, filed against a
    // file:line holding only half of it. Two unrelated same-named types must stay two types.
    [Fact]
    public void TwoSameNamedTypesInDifferentNamespacesAreNotMergedIntoOneOversizedType()
    {
        var types = Types(
            ("A/Thing.cs", Part("Sample.A", "Thing", 12)),
            ("B/Thing.cs", Part("Sample.B", "Thing", 12)));

        Assert.Equal(2, types.Count);
        Assert.All(types, t => Assert.Equal(12, t.MemberCount));
        Assert.Empty(Findings(Context(), types));
    }

    // Second non-vacuity twin. An implementation that counted every member in a FILE rather than
    // every member of a TYPE would also pass the partial test (both its files hold one type),
    // and would flag this pair of perfectly reasonable neighbours as one 24-member god class.
    [Fact]
    public void TwoTypesSharingOneFileAreNotCountedAsASingleOversizedType()
    {
        var source = Part("Sample", "First", 12) + "\n\n"
                     + $"public class Second\n{{\n{Members(12, 12)}\n}}";

        var types = Types(("Pair.cs", source));

        Assert.Equal(2, types.Count);
        Assert.All(types, t => Assert.Equal(12, t.MemberCount));
        Assert.Empty(Findings(Context(), types));
    }

    static string LongMethod(int index) =>
        $"    public int Long{index}()\n    {{\n"
        + string.Join("\n", Enumerable.Range(0, 40).Select(i => $"        var v{i} = {i};"))
        + $"\n        return {index};\n    }}";

    // The second trigger has to be independent of the first or it is decoration. This type has
    // 6 members - comfortably inside the member limit - but 4 of them each exceed the profile's
    // method-length limit, which is a decomposition that failed systematically rather than one
    // method needing an extraction. Member count alone never sees it.
    [Fact]
    public void ACompactButUnderDecomposedTypeIsFlaggedOnLongMethodDensityAlone()
    {
        var source = $$"""
            namespace Sample;

            public class Dense
            {
            {{string.Join("\n\n", Enumerable.Range(0, 4).Select(LongMethod))}}
                public int A() => 1;
                public int B() => 2;
            }
            """;

        var dense = Assert.Single(Types(("Dense.cs", source)));
        Assert.Equal(6, dense.MemberCount);

        var finding = Assert.Single(Findings(Context(), new[] { dense }));

        Assert.Contains("4 methods over 35 lines (limit 3)", finding.Evidence);
        Assert.DoesNotContain("members (limit", finding.Evidence);
    }

    [Fact]
    public void ThresholdsComeFromTheProfile()
    {
        var relaxed = new Profile { MaxTypeMembers = 1000, MaxLongMethodsPerType = 1000 };

        Assert.Empty(Findings(Context(relaxed), Types(("Big.cs", Part("Sample", "Big", 40)))));
    }

    // A reader of the TEXT report gets one line per finding, so if the breakdown lived only in
    // the JSON the text report would state a 24-member, N-line type at a single file:line and
    // read as if it were that size in one place. The evidence names every declaring file.
    [Fact]
    public void EvidenceNamesEveryDeclaringFileOfAPartialType()
    {
        var finding = Assert.Single(Findings(Context(), Types(
            ("Big.cs", Part("Sample", "Big", 12)),
            ("Big.Extra.cs", Part("Sample", "Big", 12, startAt: 12)))));

        Assert.Contains("across 2 files", finding.Evidence);
        Assert.Contains("Big.cs:", finding.Evidence);
        Assert.Contains("Big.Extra.cs:", finding.Evidence);
    }

    [Fact]
    public void SingleFileTypeEvidenceStatesOneFileWithoutABreakdownList()
    {
        var finding = Assert.Single(Findings(Context(), Types(("Big.cs", Part("Sample", "Big", 40)))));

        Assert.Contains("across 1 file", finding.Evidence);
        Assert.DoesNotContain("(Big.cs:", finding.Evidence);
        Assert.Equal("Big.cs", finding.FilePath);
        Assert.Equal(3, finding.Line);
        Assert.Equal(1.0, finding.Confidence);
        Assert.Equal("default", finding.Profile);
    }
}
