using CodeQuality.Core.Model;
using CodeQuality.Core.Parsing;
using Xunit;

namespace CodeQuality.Tests.Parsing;

public class CommentBlockAttachmentTruthTableTests
{
    [Fact]
    public void Row1_LeadingCommentsWithNoCodeBetweenMergeIntoOneBlock()
    {
        const string source = """
            namespace Sample;

            public class Worker
            {
                public void Run()
                {
                    // A
                    // B
                    Foo();
                }
            }
            """;

        var line = SourceFileParser.ParseComments("Worker.cs", source)
            .Where(b => b.Kind == CommentKind.Line)
            .ToList();

        var block = Assert.Single(line);
        Assert.Equal("A\nB", block.Text);
    }

    [Fact]
    public void Row2_TrailingCommentDoesNotMergeWithNextStatementsLeadingComment()
    {
        const string source = """
            namespace Sample;

            public class Worker
            {
                public void Run()
                {
                    Foo(); // A
                    // B
                    Bar();
                }
            }
            """;

        var line = SourceFileParser.ParseComments("Worker.cs", source)
            .Where(b => b.Kind == CommentKind.Line)
            .OrderBy(b => b.StartLine)
            .ToList();

        Assert.Equal(2, line.Count);
        Assert.Equal("A", line[0].Text);
        Assert.Equal("B", line[1].Text);
    }

    [Fact]
    public void Row3_LeadingCommentDoesNotMergeWithTheSameStatementsTrailingComment()
    {
        const string source = """
            namespace Sample;

            public class Worker
            {
                public void Run()
                {
                    // A
                    Foo(); // B
                    Bar();
                }
            }
            """;

        var line = SourceFileParser.ParseComments("Worker.cs", source)
            .Where(b => b.Kind == CommentKind.Line)
            .OrderBy(b => b.StartLine)
            .ToList();

        Assert.Equal(2, line.Count);
        Assert.Equal("A", line[0].Text);
        Assert.Equal("B", line[1].Text);
    }

    [Fact]
    public void Row4_LeadingCommentDoesNotMergeWithATrailingCommentOfAnUnrelatedStatementOnTheNextLine()
    {
        const string source = """
            namespace Sample;

            public class Worker
            {
                public void Run()
                {
                    // A
                    Foo(); Bar(); // B
                }
            }
            """;

        var line = SourceFileParser.ParseComments("Worker.cs", source)
            .Where(b => b.Kind == CommentKind.Line)
            .OrderBy(b => b.StartLine)
            .ToList();

        Assert.Equal(2, line.Count);
        Assert.Equal("A", line[0].Text);
        Assert.Equal("B", line[1].Text);
    }

    [Fact]
    public void Row5_TwoStatementsEachWithTheirOwnTrailingCommentNeverMerge()
    {
        const string source = """
            namespace Sample;

            public class Worker
            {
                public void Run()
                {
                    Foo(); // A
                    Bar(); // B
                }
            }
            """;

        var line = SourceFileParser.ParseComments("Worker.cs", source)
            .Where(b => b.Kind == CommentKind.Line)
            .OrderBy(b => b.StartLine)
            .ToList();

        Assert.Equal(2, line.Count);
        Assert.Equal("A", line[0].Text);
        Assert.Equal("B", line[1].Text);
    }

    [Fact]
    public void Row6_BlankLineBetweenLeadingCommentGroupsStillSplitsThemIntoTwoBlocks()
    {
        const string source = """
            namespace Sample;

            public class Worker
            {
                public void Run()
                {
                    // A

                    // B
                    Foo();
                }
            }
            """;

        var line = SourceFileParser.ParseComments("Worker.cs", source)
            .Where(b => b.Kind == CommentKind.Line)
            .OrderBy(b => b.StartLine)
            .ToList();

        Assert.Equal(2, line.Count);
        Assert.Equal("A", line[0].Text);
        Assert.Equal("B", line[1].Text);
    }

    [Fact]
    public void Row7_DifferentCommentKindsNeverMergeEvenOnAdjacentLines()
    {
        const string source = """
            namespace Sample;

            public class Worker
            {
                // A
                /// B
                public void Run()
                {
                    Foo();
                }
            }
            """;

        var blocks = SourceFileParser.ParseComments("Worker.cs", source);

        Assert.Equal(2, blocks.Count);
        Assert.Contains(blocks, b => b.Kind == CommentKind.Line && b.Text == "A");
        Assert.Contains(blocks, b => b.Kind == CommentKind.Xml && b.Text.Contains("B"));
    }

    [Fact]
    public void Row8_MultiLineBlockCommentSpanningThreeLinesIsOneBlock()
    {
        const string source = """
            namespace Sample;

            public class Worker
            {
                public void Run()
                {
                    /* line one
                       line two
                       line three */
                    Foo();
                }
            }
            """;

        var line = SourceFileParser.ParseComments("Worker.cs", source)
            .Where(b => b.Kind == CommentKind.Line)
            .ToList();

        Assert.Single(line);
    }

    [Fact]
    public void Row9_XmlDocBlockOfThreeSlashLinesIsOneBlock()
    {
        const string source = """
            namespace Sample;

            public class Worker
            {
                /// line one
                /// line two
                /// line three
                public void Run()
                {
                }
            }
            """;

        var xml = SourceFileParser.ParseComments("Worker.cs", source)
            .Where(b => b.Kind == CommentKind.Xml)
            .ToList();

        Assert.Single(xml);
    }
}
