using CodeQuality.Core.Model;
using CodeQuality.Core.Parsing;
using Xunit;

namespace CodeQuality.Tests.Parsing;

public class CommentBlockHostileInputTests
{
    [Fact]
    public void EmptyFileYieldsNoBlocks()
    {
        var blocks = SourceFileParser.ParseComments("Empty.cs", string.Empty);

        Assert.Empty(blocks);
    }

    [Fact]
    public void FileThatIsOnlyACommentYieldsOneBlock()
    {
        var blocks = SourceFileParser.ParseComments("OnlyComment.cs", "// just a comment");

        var block = Assert.Single(blocks);
        Assert.Equal("just a comment", block.Text);
        Assert.Null(block.EnclosingMethod);
    }

    [Fact]
    public void UnterminatedBlockCommentDoesNotThrow()
    {
        var blocks = SourceFileParser.ParseComments("Unterminated.cs", "/* unterminated block");

        var block = Assert.Single(blocks);
        Assert.Equal(CommentKind.Line, block.Kind);
    }

    [Fact]
    public void TrailingLineCommentWithNoNewlineDoesNotThrow()
    {
        var blocks = SourceFileParser.ParseComments(
            "NoTrailingNewline.cs",
            "namespace X; class C { void M() { } } // trailing");

        var block = Assert.Single(blocks);
        Assert.Equal("trailing", block.Text);
    }
}
