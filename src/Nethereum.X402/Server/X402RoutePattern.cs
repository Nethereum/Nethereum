using System.Text;
using System.Text.RegularExpressions;

namespace Nethereum.X402.Server;

internal static class X402RoutePattern
{
    private const string SpecialCharacters = "$()+.?^{|}";

    public static Regex Compile(string pathPattern)
    {
        var path = NormalizePath(pathPattern);
        var trailingWildcard = path.EndsWith("/*", StringComparison.Ordinal);
        var body = trailingWildcard ? path.Substring(0, path.Length - 2) : path;

        var regex = new StringBuilder("^");
        regex.Append(TranslateBody(body));
        if (trailingWildcard)
            regex.Append("(?:/.*?)?");
        regex.Append("\\z");

        return new Regex(
            regex.ToString(),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.NonBacktracking);
    }

    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return "/";

        var collapsed = new StringBuilder(path.Length);
        foreach (var c in path)
        {
            if (c == '/' && collapsed.Length > 0 && collapsed[collapsed.Length - 1] == '/')
                continue;
            collapsed.Append(c);
        }

        while (collapsed.Length > 1 && collapsed[collapsed.Length - 1] == '/')
            collapsed.Length--;

        return collapsed.ToString();
    }

    public static IReadOnlyList<string> CandidatePaths(string? path)
    {
        var normalized = NormalizePath(path);
        var resolved = ResolveHostSpelling(path);
        return string.Equals(normalized, resolved, StringComparison.Ordinal)
            ? new[] { normalized }
            : new[] { normalized, resolved };
    }

    public static int ComparePrecedence(string left, string right)
    {
        var a = Describe(left);
        var b = Describe(right);

        if (a.HasWildcard != b.HasWildcard)
            return a.HasWildcard ? 1 : -1;

        for (var i = 0; i < Math.Max(a.Ranks.Count, b.Ranks.Count); i++)
        {
            var ra = i < a.Ranks.Count ? a.Ranks[i] : 0;
            var rb = i < b.Ranks.Count ? b.Ranks[i] : 0;
            if (ra != rb)
                return rb.CompareTo(ra);
        }

        return b.Ranks.Count(r => r == LiteralRank).CompareTo(a.Ranks.Count(r => r == LiteralRank));
    }

    private const int LiteralRank = 3;
    private const int ParameterRank = 2;
    private const int WildcardRank = 1;

    private static (bool HasWildcard, List<int> Ranks) Describe(string pattern)
    {
        var segments = NormalizePath(pattern).Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        var hasWildcard = segments.Any(segment => segment.Contains('*'));
        if (segments.Count > 0 && segments[segments.Count - 1] == "*")
            segments.RemoveAt(segments.Count - 1);

        return (hasWildcard, segments.Select(RankOf).ToList());
    }

    private static int RankOf(string segment)
    {
        if (segment.Contains('*'))
            return WildcardRank;

        for (var i = 0; i < segment.Length; i++)
        {
            if (segment[i] == ':' && i + 1 < segment.Length && IsIdentifierStart(segment[i + 1]))
                return ParameterRank;

            if (segment[i] == '[' && segment.IndexOf(']', i + 1) > i + 1)
                return ParameterRank;
        }

        return LiteralRank;
    }

    private static string ResolveHostSpelling(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return "/";

        var unified = path
            .Replace("%2F", "/", StringComparison.OrdinalIgnoreCase)
            .Replace("%5C", "/", StringComparison.OrdinalIgnoreCase)
            .Replace('\\', '/');

        var kept = new List<string>();
        foreach (var segment in unified.Split('/'))
        {
            if (segment == ".")
                continue;

            if (segment == "..")
            {
                if (kept.Count > 0)
                    kept.RemoveAt(kept.Count - 1);
                continue;
            }

            var trimmed = segment.TrimEnd('.', ' ');
            if (trimmed.Length > 0)
                kept.Add(trimmed);
        }

        return NormalizePath("/" + string.Join("/", kept));
    }

    private static string TranslateBody(string body)
    {
        var result = new StringBuilder(body.Length * 2);
        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];
            if (c == '\\')
            {
                result.Append("\\\\");
            }
            else if (SpecialCharacters.IndexOf(c) >= 0)
            {
                result.Append('\\').Append(c);
            }
            else if (c == '*')
            {
                result.Append(".*?");
            }
            else if (c == '[')
            {
                var close = body.IndexOf(']', i + 1);
                if (close > i + 1)
                {
                    result.Append("[^/]+");
                    i = close;
                }
                else
                {
                    result.Append("\\[");
                }
            }
            else if (c == ']')
            {
                result.Append("\\]");
            }
            else if (c == ':' && i + 1 < body.Length && IsIdentifierStart(body[i + 1]))
            {
                i++;
                while (i + 1 < body.Length && IsIdentifierPart(body[i + 1]))
                    i++;
                result.Append("[^/]+");
            }
            else if (c == '/')
            {
                result.Append("\\/");
            }
            else
            {
                result.Append(Regex.Escape(c.ToString()));
            }
        }

        return result.ToString();
    }

    private static bool IsIdentifierStart(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';

    private static bool IsIdentifierPart(char c) => IsIdentifierStart(c) || (c >= '0' && c <= '9');
}
