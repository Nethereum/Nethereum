using CodeQuality.Core.Model;
using CodeQuality.Core.Parsing;
using Xunit;

namespace CodeQuality.Tests.Parsing;

public class CommentBlockAdjacencyTests
{
    [Fact]
    public void TrailingCommentDoesNotMergeWithTheNextStatementsLeadingComment()
    {
        const string source = """
            namespace Sample;

            public class Worker
            {
                public void Run()
                {
                    Foo(); // why we call foo first
                    // Bar is called next for a reason
                    Bar();
                }
            }
            """;

        var line = SourceFileParser.ParseComments("Worker.cs", source)
            .Where(b => b.Kind == CommentKind.Line)
            .OrderBy(b => b.StartLine)
            .ToList();

        Assert.Equal(2, line.Count);
        Assert.Equal("why we call foo first", line[0].Text);
        Assert.Equal("Bar is called next for a reason", line[1].Text);
    }

    [Fact]
    public void AdjacentLeadingCommentsWithNoCodeBetweenThemStillMergeIntoOneBlock()
    {
        const string source = """
            namespace Sample;

            public class Worker
            {
                public void Run()
                {
                    // first note
                    // second note
                    Bar();
                }
            }
            """;

        var line = SourceFileParser.ParseComments("Worker.cs", source)
            .Where(b => b.Kind == CommentKind.Line)
            .ToList();

        var block = Assert.Single(line);
        Assert.Equal("first note\nsecond note", block.Text);
    }

    [Fact]
    public void BlankLineBetweenCommentGroupsStillSplitsThemIntoSeparateBlocks()
    {
        const string source = """
            namespace Sample;

            public class Worker
            {
                public void Run()
                {
                    // first group

                    // second group
                    Bar();
                }
            }
            """;

        var line = SourceFileParser.ParseComments("Worker.cs", source)
            .Where(b => b.Kind == CommentKind.Line)
            .OrderBy(b => b.StartLine)
            .ToList();

        Assert.Equal(2, line.Count);
        Assert.Equal("first group", line[0].Text);
        Assert.Equal("second group", line[1].Text);
    }
}
