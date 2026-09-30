using System.Reflection;
using System.Text.RegularExpressions;
using CodeQuality.Core.Analysis;
using Xunit;

namespace CodeQuality.Tests.Architecture;

public class StandaloneTests
{
    static readonly string[] AllowedPrefixes =
    {
        "System", "netstandard", "Microsoft.CodeAnalysis", "Microsoft.Data.Sqlite",
        "SQLitePCLRaw", "YamlDotNet", "CodeQuality",
    };

    [Fact]
    public void CoreReferencesNothingOutsideTheAllowedSet()
    {
        var referenced = typeof(PackageAnalyser).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => !AllowedPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(referenced);
    }

    [Fact]
    public void CoreDoesNotReferenceMsBuildOrWorkspaces()
    {
        var names = typeof(PackageAnalyser).Assembly.GetReferencedAssemblies().Select(a => a.Name!);

        Assert.DoesNotContain(names, n => n.Contains("MSBuild", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("Workspaces", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EngineSourceCarriesNoDomainVocabulary()
    {
        var coreDirectory = FindDirectory("CodeQuality.Core");

        // Ruling P15: substring matching false-positives on ordinary English — "together"
        // contains "geth". Word boundaries are required or this gate fails on innocent prose.
        var forbidden = new Regex(@"\b(geth|EELS|Nethereum|EIP-\d+)\b", RegexOptions.IgnoreCase);

        var offenders = Directory.EnumerateFiles(coreDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(f => (File: f, Text: File.ReadAllText(f)))
            .Where(x => forbidden.IsMatch(x.Text))
            .Select(x => Path.GetFileName(x.File))
            .ToList();

        Assert.Empty(offenders);
    }

    static string FindDirectory(string name)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, name);
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException(name);
    }
}
