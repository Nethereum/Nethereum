using System.Text.RegularExpressions;

namespace CodeQuality.Core.Config;

public static class ProfileResolver
{
    public static string Resolve(CodeQualityConfig config, string path)
    {
        var normalised = path.Replace('\\', '/');

        foreach (var binding in config.Bindings)
        {
            if (Regex.IsMatch(normalised, GlobToRegex(binding.Glob), RegexOptions.IgnoreCase))
                return binding.Profile;
        }

        return "default";
    }

    // Regex.Escape neutralises every regex metacharacter first, so the four substitutions below
    // can only ever match the literal `\*`/`\?` sequences that escaping itself produced from a
    // source `*`/`?` — they cannot recombine with anything already escaped (a literal `.`, `(`,
    // `+`, `[`, backslash, ...) to build an invalid pattern.
    static string GlobToRegex(string glob)
    {
        var escaped = Regex.Escape(glob.Replace('\\', '/'))
            .Replace(@"\*\*/", "(.*/)?")
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*")
            .Replace(@"\?", ".");
        return $"^{escaped}$";
    }
}
