#:property PublishAot=false

using System.Text;
using System.Text.RegularExpressions;

if (args.Length == 2 && args[0] == "build-filter") return BuildFilter(args[1]);
if (args.Length == 3 && args[0] == "summary-diff") return SummaryDiff(args[1], args[2]);
Console.Error.WriteLine("usage: dotnet run ci/run-tests-tools.cs -- build-filter <out.slnf> | summary-diff <previous> <current>");
return 2;

static string NormPath(string path)
{
    var parts = new List<string>();
    foreach (var part in path.Split('/'))
    {
        if (part.Length == 0 || part == ".") continue;
        if (part == ".." && parts.Count > 0 && parts[^1] != "..") parts.RemoveAt(parts.Count - 1);
        else parts.Add(part);
    }
    return parts.Count == 0 ? "." : string.Join("/", parts);
}

static string ProjectName(string path) => Path.GetFileNameWithoutExtension(path);

static string JsonString(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

static List<string> References(string project)
{
    if (!File.Exists(project)) return new List<string>();
    var text = File.ReadAllText(project);
    var directory = Path.GetDirectoryName(project)!.Replace('\\', '/');
    return Regex.Matches(text, "ProjectReference Include=\"([^\"]+)\"")
        .Select(m => NormPath(directory + "/" + m.Groups[1].Value.Replace('\\', '/')))
        .ToList();
}

static bool DependsOnSkipped(string project, HashSet<string> skip, HashSet<string> seen)
{
    foreach (var reference in References(project))
    {
        if (!seen.Add(reference)) continue;
        if (skip.Contains(ProjectName(reference)) || DependsOnSkipped(reference, skip, seen)) return true;
    }
    return false;
}

static int BuildFilter(string output)
{
    var skip = File.ReadAllLines("ci/skip-build.txt").Select(l => l.Trim()).Where(l => l.Length > 0).ToHashSet();
    var paths = Regex.Matches(File.ReadAllText("Nethereum.slnx"), "Path=\"([^\"]+\\.csproj)\"")
        .Select(m => m.Groups[1].Value.Replace('\\', '/'))
        .Distinct()
        .OrderBy(p => p, StringComparer.Ordinal)
        .ToList();
    var dependents = paths.Where(p => !skip.Contains(ProjectName(p)) && DependsOnSkipped(p, skip, new HashSet<string>())).ToList();
    var keep = paths.Where(p => !skip.Contains(ProjectName(p)) && !dependents.Contains(p)).ToList();

    var json = new StringBuilder();
    json.Append("{\n \"solution\": {\n  \"path\": ").Append(JsonString(Path.GetFullPath("Nethereum.slnx"))).Append(",\n  \"projects\": ");
    if (keep.Count == 0) json.Append("[]");
    else json.Append("[\n").Append(string.Join(",\n", keep.Select(p => "   " + JsonString(p)))).Append("\n  ]");
    json.Append("\n }\n}");
    File.WriteAllText(output, json.ToString(), new UTF8Encoding(false));

    Console.WriteLine($"{keep.Count} projects, {paths.Count - keep.Count} skipped by ci/skip-build.txt"
        + (dependents.Count > 0 ? " (dependents: " + string.Join(", ", dependents.Select(ProjectName)) + ")" : ""));
    return 0;
}

static Dictionary<string, string> ParseSummary(string path)
{
    var rows = new Dictionary<string, string>();
    foreach (var line in File.ReadAllLines(path))
    {
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || parts[0] == "START" || parts[0] == "DONE" || parts[0] == "SKIP") continue;
        var match = Regex.Match(line, @"Failed:\s+(\d+), Passed:\s+(\d+)");
        string value;
        if (match.Success) value = match.Groups[1].Value + "F/" + match.Groups[2].Value + "P";
        else
        {
            value = string.Join(" ", parts.Skip(3));
            if (value.Length > 60) value = value[..60];
        }
        rows[parts[1] + " " + parts[2]] = value;
    }
    return rows;
}

static int SummaryDiff(string previous, string current)
{
    var before = ParseSummary(previous);
    var after = ParseSummary(current);
    var changed = after.OrderBy(kv => kv.Key, StringComparer.Ordinal)
        .Where(kv => !before.TryGetValue(kv.Key, out var old) || old != kv.Value)
        .Select(kv => (kv.Key, Before: before.TryGetValue(kv.Key, out var b) ? b : "-", After: kv.Value))
        .ToList();
    Console.WriteLine($"{changed.Count} project/framework results changed since the previous run");
    foreach (var (key, b, a) in changed)
        Console.WriteLine($"  {key.PadRight(70)} {b.PadLeft(24)} -> {a}");
    return 0;
}
