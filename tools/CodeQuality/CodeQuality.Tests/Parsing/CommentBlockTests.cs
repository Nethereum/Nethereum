using CodeQuality.Core.Model;
using CodeQuality.Core.Parsing;
using Xunit;

namespace CodeQuality.Tests.Parsing;

public class CommentBlockTests
{
    const string Source = """
        namespace Sample;

        public class Worker
        {
            /// <summary>
            /// Does the work.
            /// </summary>
            public void Run()
            {
                // A peer whose head sits below the pivot
                // simply has not synced yet.
                Wait();

                // Unrelated single line.
                Go();
            }
        }
        """;

    [Fact]
    public void GroupsContiguousLinesIntoOneBlock()
    {
        var blocks = SourceFileParser.ParseComments("Worker.cs", Source);
        var line = blocks.Where(b => b.Kind == CommentKind.Line).ToList();

        Assert.Equal(2, line.Count);
        Assert.Contains("simply has not synced yet", line[0].Text);
        Assert.Equal("Unrelated single line.", line[1].Text);
    }

    [Fact]
    public void KeepsXmlDocSeparateFromLineComments()
    {
        var blocks = SourceFileParser.ParseComments("Worker.cs", Source);

        var xml = Assert.Single(blocks, b => b.Kind == CommentKind.Xml);
        Assert.Contains("Does the work.", xml.Text);
    }

    [Fact]
    public void AttributesBlocksToTheEnclosingMethod()
    {
        var blocks = SourceFileParser.ParseComments("Worker.cs", Source);

        Assert.All(blocks, b => Assert.Equal("Run", b.EnclosingMethod));
    }

    [Fact]
    public void DistinguishesBodyCommentsFromLeadingDocumentation()
    {
        var blocks = SourceFileParser.ParseComments("Worker.cs", Source);

        Assert.False(blocks.Single(b => b.Kind == CommentKind.Xml).IsInsideMethodBody);
        Assert.All(blocks.Where(b => b.Kind == CommentKind.Line),
            b => Assert.True(b.IsInsideMethodBody));
    }

    [Fact]
    public void RecordsOneBasedStartAndEndLines()
    {
        var block = SourceFileParser.ParseComments("Worker.cs", Source)
            .First(b => b.Kind == CommentKind.Line);

        Assert.Equal(10, block.StartLine);
        Assert.Equal(11, block.EndLine);
    }
}
