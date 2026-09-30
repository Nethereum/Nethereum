using CodeQuality.Core.Analysis;
using CodeQuality.Core.Config;
using CodeQuality.Core.Model;
using CodeQuality.Core.Storage;

namespace CodeQuality.Cli;

public static class Commands
{
    // Ruling P4: `--config` overrides upward discovery so a run never requires dropping a config
    // file into the analysed repository's root and deleting it afterwards.
    public static int Summary(string path, bool json, string? configPath)
    {
        var result = Analyse(path, configPath);
        Console.WriteLine(json ? JsonRenderer.Summary(result) : ReportRenderer.Summary(result));
        Persist(path, result, configPath);
        return result.Findings.Count == 0 ? 0 : 1;
    }

    public static int Methods(string path, int minLines, bool json, string? configPath)
    {
        var result = Analyse(path, configPath);
        var methods = result.Methods
            .Where(m => m.LineCount >= minLines)
            .OrderByDescending(m => m.LineCount)
            .ToList();

        Console.WriteLine(json
            ? JsonRenderer.Methods(result, methods)
            : string.Join("\n", methods.Select(m =>
                $"{m.FilePath}:{m.Line}  {m.ContainingType}.{m.Name}  {m.LineCount} lines")));

        return methods.Count == 0 ? 0 : 1;
    }

    public static int Clones(string path, bool json, string? configPath)
    {
        var result = Analyse(path, configPath);
        var findings = result.Findings.Where(f => f.Kind == FindingKind.Clone).ToList();

        Console.WriteLine(json
            ? JsonRenderer.Findings(result, findings)
            : string.Join("\n", findings.Select(f => $"{f.FilePath}:{f.Line}  {f.Evidence}")));

        return findings.Count == 0 ? 0 : 1;
    }

    public static int Comments(string path, string? verdictFilter, bool json, string? configPath)
    {
        var result = Analyse(path, configPath);
        var findings = result.Findings
            .Where(f => f.Kind is FindingKind.Comment or FindingKind.Reference)
            .Where(f => verdictFilter is null
                        || f.Verdict.ToString().Equals(verdictFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Console.WriteLine(json
            ? JsonRenderer.Findings(result, findings)
            : string.Join("\n", findings.Select(f => $"{f.FilePath}:{f.Line}  {f.Verdict}  {f.Evidence}")));

        return findings.Count == 0 ? 0 : 1;
    }

    internal static CodeQualityConfig LoadConfig(string path, string? configPath)
    {
        if (configPath is null) return ConfigLoader.Discover(path);

        var config = ConfigLoader.LoadFrom(File.ReadAllText(configPath));
        config.SourcePath = configPath;
        return config;
    }

    static AnalysisResult Analyse(string path, string? configPath) =>
        new PackageAnalyser(LoadConfig(path, configPath)).Analyse(path);

    static void Persist(string path, AnalysisResult result, string? configPath)
    {
        var directory = Path.Combine(path, ".codequality");
        Directory.CreateDirectory(directory);
        var store = new FindingStore(Path.Combine(directory, "verdicts.db"));
        var rulesVersion = LoadConfig(path, configPath).RulesVersion;

        // Ruling P21: RulesVersion hashes the config text, so even a cosmetic reformat resets every
        // recorded decision. That is the safe direction, but it must not be silent — an expensive
        // re-triage triggered by a stray comment should announce itself.
        var dropped = store.Load(result.Package).Count(f => f.Decision != Decision.Pending
                                                            && f.RulesVersion != rulesVersion);
        if (dropped > 0)
            Console.Error.WriteLine($"{dropped} recorded decision(s) dropped: rules version changed");

        store.Save(result.Package, result.Findings, rulesVersion);
    }
}
