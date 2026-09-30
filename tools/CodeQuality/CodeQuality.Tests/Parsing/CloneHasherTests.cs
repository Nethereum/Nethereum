using CodeQuality.Core.Parsing;
using Xunit;

namespace CodeQuality.Tests.Parsing;

public class CloneHasherTests
{
    static string Wrap(string body, string name = "M") => $$"""
        public class C
        {
            void {{name}}()
            {
        {{body}}
            }
        }
        """;

    const string Body = """
                var a = 1;
                var b = 2;
                var c = a + b;
                var d = c * 2;
                var e = d - 1;
                var f = e / 3;
                var g = f + 4;
                Console.WriteLine(g);
        """;

    static string HashOf(string source)
    {
        var method = SourceFileParser.ParseMethods("f.cs", source).Single();
        return CloneHasher.Hash(source, method, minStatements: 8)!;
    }

    [Fact]
    public void IdenticalBodiesHashEqual() =>
        Assert.Equal(HashOf(Wrap(Body)), HashOf(Wrap(Body, "Other")));

    [Fact]
    public void CommentsAndWhitespaceDoNotAffectTheHash()
    {
        var noisy = Wrap("        // explanation\n\n" + Body);
        Assert.Equal(HashOf(Wrap(Body)), HashOf(noisy));
    }

    [Fact]
    public void StringLiteralsAreNormalised()
    {
        var one = Wrap(Body.Replace("Console.WriteLine(g);", "Console.WriteLine(\"alpha\");"));
        var two = Wrap(Body.Replace("Console.WriteLine(g);", "Console.WriteLine(\"beta\");"));
        Assert.Equal(HashOf(one), HashOf(two));
    }

    [Fact]
    public void DifferentLogicHashesDifferently()
    {
        var changed = Wrap(Body.Replace("var c = a + b;", "var c = a - b;"));
        Assert.NotEqual(HashOf(Wrap(Body)), HashOf(changed));
    }

    [Fact]
    public void MethodsBelowTheStatementFloorAreNotHashed()
    {
        var source = Wrap("        var a = 1;\n        var b = 2;");
        var method = SourceFileParser.ParseMethods("f.cs", source).Single();
        Assert.Null(CloneHasher.Hash(source, method, minStatements: 8));
    }

    [Fact]
    public void GroupsSevenIdenticalCopiesAsOneGroupOfSeven()
    {
        var hashed = Enumerable.Range(0, 7)
            .Select(i =>
            {
                var source = Wrap(Body, $"Copy{i}");
                var method = SourceFileParser.ParseMethods($"File{i}.cs", source).Single();
                return (Hash: CloneHasher.Hash(source, method, 8)!, Method: method);
            })
            .ToList();

        var group = Assert.Single(CloneHasher.Group(hashed));
        Assert.Equal(7, group.Members.Count);
    }

    [Fact]
    public void SingletonsAreNotReportedAsGroups()
    {
        var source = Wrap(Body);
        var method = SourceFileParser.ParseMethods("f.cs", source).Single();
        var hashed = new[] { (CloneHasher.Hash(source, method, 8)!, method) };

        Assert.Empty(CloneHasher.Group(hashed));
    }
}
