namespace CodeQuality.Cli;

public sealed record ParsedArguments(bool Json, string? Config, int MinLines, string? Verdict);

public static class ArgumentParser
{
    static readonly HashSet<string> ValuedFlags = new() { "--config", "--min-lines", "--verdict" };

    // Everything else that starts with "--" and is not one of the flags below is a typo the run
    // must not silently absorb: `--confg` reads as "no config given" the same way a missing
    // ruleset does, and the two failures look identical in the report unless the flag itself is
    // rejected before analysis starts.
    public static ParsedArguments Parse(IReadOnlyList<string> args)
    {
        var json = false;
        string? config = null;
        string? verdict = null;
        var minLines = 0;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal)) continue;

            if (arg == "--json") { json = true; continue; }

            if (!ValuedFlags.Contains(arg))
                throw new ArgumentException($"unknown flag: {arg}");

            var value = RequireValue(args, arg, i);
            i++;

            switch (arg)
            {
                case "--config": config = value; break;
                case "--verdict": verdict = value; break;
                case "--min-lines": minLines = ParseMinLines(value); break;
            }
        }

        return new ParsedArguments(json, config, minLines, verdict);
    }

    static string RequireValue(IReadOnlyList<string> args, string flag, int index) =>
        index + 1 < args.Count
            ? args[index + 1]
            : throw new ArgumentException($"{flag} requires a value");

    static int ParseMinLines(string value) =>
        int.TryParse(value, out var parsed)
            ? parsed
            : throw new ArgumentException($"--min-lines requires a numeric value, got '{value}'");
}
