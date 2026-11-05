using System.Text;

namespace Nethereum.X402.AspNetCore;

internal static class X402CacheControl
{
    public static string WithPrivate(string existing)
    {
        var kept = new List<string>();
        var hasPrivate = false;
        foreach (var directive in SplitDirectives(existing))
        {
            var name = directive.Split('=')[0].Trim();
            var qualified = directive.Contains('=');
            if (name.Equals("public", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("s-maxage", StringComparison.OrdinalIgnoreCase) ||
                (qualified && name.Equals("private", StringComparison.OrdinalIgnoreCase)))
                continue;

            hasPrivate |= name.Equals("private", StringComparison.OrdinalIgnoreCase);
            kept.Add(directive);
        }

        if (!hasPrivate)
            kept.Add("private");

        return string.Join(", ", kept);
    }

    private static IEnumerable<string> SplitDirectives(string value)
    {
        var current = new StringBuilder();
        var quoted = false;
        foreach (var c in value)
        {
            if (c == '"')
                quoted = !quoted;

            if (c == ',' && !quoted)
            {
                if (current.ToString().Trim().Length > 0)
                    yield return current.ToString().Trim();
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (current.ToString().Trim().Length > 0)
            yield return current.ToString().Trim();
    }
}
