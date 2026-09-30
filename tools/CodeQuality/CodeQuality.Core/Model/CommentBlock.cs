namespace CodeQuality.Core.Model;

public enum CommentKind { Line, Xml }

public sealed record CommentBlock(
    string FilePath,
    int StartLine,
    int EndLine,
    string Text,
    CommentKind Kind,
    string? EnclosingMethod,
    bool IsInsideMethodBody);
