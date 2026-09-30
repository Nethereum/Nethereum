using CodeQuality.Core.Model;
using CodeQuality.Core.Parsing;
using Xunit;

namespace CodeQuality.Tests.Parsing;

public class TypeMetricsTests
{
    static IReadOnlyList<TypeMetrics> Types(int maxMethodLines, params (string File, string Source)[] files) =>
        TypeWalker.Merge(
            files.SelectMany(f => TypeWalker.Collect(f.File, f.Source, maxMethodLines)),
            maxMethodLines);

    const string MemberKindsSource = """
        namespace Sample;

        public class Bag
        {
            public event EventHandler Opened;
            public event EventHandler First, Second;
            int _a, _b, _c;
            const string Name = "x";

            public Bag() { }
            public Bag(int seed) { }

            public int Value { get; set; }
            public int this[int index] => index;

            public void Do() { }
            public static Bag operator +(Bag left, Bag right) => left;
            ~Bag() { }
        }
        """;

    // Ground truth, read off the source above: 3 event variables (Opened, First, Second),
    // 4 field variables (_a, _b, _c and Name - a `const` is a field declaration too),
    // 2 constructors, 2 properties (Value and the indexer), 3 methods (Do, operator +, the
    // destructor). A counter that treated `int _a, _b, _c;` as ONE field, or dropped operators
    // and destructors, would produce a smaller number here and under-report every large type.
    [Fact]
    public void CountsEachDeclaredVariableAndEveryMethodLikeMemberSeparately()
    {
        var bag = Types(35, ("Bag.cs", MemberKindsSource)).Single();

        Assert.Equal(3, bag.EventCount);
        Assert.Equal(4, bag.FieldCount);
        Assert.Equal(2, bag.ConstructorCount);
        Assert.Equal(2, bag.PropertyCount);
        Assert.Equal(3, bag.MethodCount);
        Assert.Equal(14, bag.MemberCount);
        Assert.Equal("class", bag.Kind);
        Assert.False(bag.IsPartial);
    }

    const string NestedSource = """
        namespace Sample;

        public class Outer
        {
            public void OuterOne() { }

            public class Inner
            {
                public void InnerOne() { }
                public void InnerTwo() { }
            }
        }
        """;

    // A nested type is its own unit of work with its own name, so charging its members to the
    // type that merely encloses it would inflate the outer type and hide the inner one. The
    // inner type's name is qualified through the chain so the two can never collide.
    [Fact]
    public void NestedTypeMembersAreChargedToTheNestedTypeNotItsEnclosingType()
    {
        var types = Types(35, ("Outer.cs", NestedSource));

        var outer = types.Single(t => t.FullName == "Sample.Outer");
        var inner = types.Single(t => t.FullName == "Sample.Outer.Inner");

        Assert.Equal(1, outer.MethodCount);
        Assert.Equal(2, inner.MethodCount);
    }

    const string ArityA = """
        namespace Sample;
        public partial class Cache { public void One() { } }
        """;

    const string ArityB = """
        namespace Sample;
        public partial class Cache<T> { public void Two() { } }
        """;

    // `Cache` and `Cache<T>` are two types that legally coexist. Keying the merge on the simple
    // name would fuse them into one entry with both members - an aggregate for a type that does
    // not exist, reported against a file:line that only holds half of it.
    [Fact]
    public void GenericArityKeepsTwoCoexistingTypesFromMergingIntoOne()
    {
        var types = Types(35, ("A.cs", ArityA), ("B.cs", ArityB));

        Assert.Equal(2, types.Count);
        Assert.All(types, t => Assert.Equal(1, t.MethodCount));
        Assert.Contains(types, t => t.FullName == "Sample.Cache");
        Assert.Contains(types, t => t.FullName == "Sample.Cache`1");
    }

    const string AccessorSource = """
        namespace Sample;

        public class Widget
        {
            public int Auto { get; set; }

            public int Computed
            {
                get
                {
                    var a = 1;
                    var b = 2;
                    return a + b;
                }
            }

            public void Short() { }
        }
        """;

    // LongMethodCount has to range over exactly the declarations structure.long-method ranges
    // over, or the two numbers disagree and the density figure means nothing. Deriving BOTH
    // sides here rather than hardcoding either means an off-by-one in the type walker's line
    // span - the easiest way to get this wrong - fails the test rather than shifting both
    // numbers together.
    [Fact]
    public void LongMethodCountAgreesWithTheMethodParserOnTheSameDeclarations()
    {
        const int limit = 4;

        var widget = Types(limit, ("Widget.cs", AccessorSource)).Single();
        var fromMethodParser = SourceFileParser.ParseMethods("Widget.cs", AccessorSource)
            .Count(m => m.ContainingType == "Widget" && m.LineCount > limit);

        // The `Computed` getter spans 6 lines (`get` through its closing brace) and is the only
        // declaration above the limit; `Short` is one line and the auto-property has no accessor
        // bodies at all.
        Assert.Equal(1, widget.LongMethodCount);
        Assert.Equal(fromMethodParser, widget.LongMethodCount);
        Assert.Equal(limit, widget.MaxMethodLinesApplied);
    }

    const string PartOne = """
        namespace Sample;
        public partial class Split
        {
            public void One() { }
            public void Two() { }
        }
        """;

    const string PartTwo = """
        namespace Sample;
        public partial class Split
        {
            public void Three() { }
        }
        """;

    // The primary declaration is the biggest part, because that is where a reader starts and it
    // is what a single file:line citation has to point at. Taking whichever part happened to be
    // parsed first would make the citation depend on directory enumeration order.
    [Fact]
    public void PrimaryDeclarationIsTheLargestPartNotWhicheverWasParsedFirst()
    {
        // PartOne (5 lines) is put in the ordinal-LAST file and PartTwo (4 lines) in the
        // ordinal-first one, so neither parse order nor path order can pick the right answer by
        // accident - only measuring the parts can.
        var split = Types(35, ("Alpha.cs", PartTwo), ("Zebra.cs", PartOne)).Single();

        Assert.Equal("Zebra.cs", split.PrimaryFilePath);
        Assert.Equal(2, split.DeclaringFileCount);
        Assert.Equal(new[] { "Alpha.cs", "Zebra.cs" }, split.DeclaringFiles);
        Assert.True(split.IsPartial);
        Assert.Equal(3, split.MethodCount);
    }
}
