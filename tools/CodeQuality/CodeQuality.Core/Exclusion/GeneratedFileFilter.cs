using CodeQuality.Core.Config;

namespace CodeQuality.Core.Exclusion;

public sealed class GeneratedFileFilter
{
    readonly GeneratedFileRules _rules;

    public GeneratedFileFilter(GeneratedFileRules rules) => _rules = rules;

    public bool IsGenerated(string filePath, string sourceText) =>
        MatchesFileName(filePath) || MatchesPathSegment(filePath) || MatchesHeader(sourceText);

    bool MatchesFileName(string filePath)
    {
        var name = PathSegments(filePath)[^1];
        return _rules.FileNameSuffixes.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase))
            || _rules.FileNamePrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    bool MatchesPathSegment(string filePath)
    {
        var segments = PathSegments(filePath);
        return _rules.PathSegments.Any(s => segments.Contains(s, StringComparer.OrdinalIgnoreCase));
    }

    static string[] PathSegments(string filePath) => filePath.Split('/', '\\');

    bool MatchesHeader(string sourceText)
    {
        if (_rules.HeaderMarkers.Count == 0) return false;
        var scanChars = Math.Clamp(_rules.HeaderScanChars, 0, sourceText.Length);
        var header = sourceText[..scanChars];
        return _rules.HeaderMarkers.Any(m => header.Contains(m, StringComparison.OrdinalIgnoreCase));
    }
}
