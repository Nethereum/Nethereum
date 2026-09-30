using CodeQuality.Core.Model;
using Xunit;

namespace CodeQuality.Tests.Rules;

public class FindingTests
{
    static Finding Make(int line = 42, string rule = "structure.long-method", string symbol = "HealAsync") => new(
        Package: "DevP2P.Sync",
        FilePath: "src/DevP2P.Sync/TrieHealer.cs",
        Line: line,
        Symbol: symbol,
        Kind: FindingKind.Structure,
        RuleId: rule,
        Verdict: Verdict.Review,
        Evidence: "702 lines",
        Confidence: 1.0,
        Profile: "protocol");

    [Fact]
    public void IdIsPackageFileLineSymbolRule() =>
        Assert.Equal("DevP2P.Sync:src/DevP2P.Sync/TrieHealer.cs:42:HealAsync:structure.long-method", Make().Id);

    [Fact]
    public void IdUsesForwardSlashesOnEveryPlatform()
    {
        var finding = Make() with { FilePath = @"src\DevP2P.Sync\TrieHealer.cs" };
        Assert.Equal("DevP2P.Sync:src/DevP2P.Sync/TrieHealer.cs:42:HealAsync:structure.long-method", finding.Id);
    }

    [Fact]
    public void IdIsStableAcrossEqualFindings() =>
        Assert.Equal(Make().Id, Make().Id);

    [Fact]
    public void IdDistinguishesRulesAtTheSameLocation() =>
        Assert.NotEqual(Make(rule: "structure.long-method").Id, Make(rule: "structure.deep-nesting").Id);

    [Fact]
    public void IdDistinguishesSymbolsAtTheSameLocation() =>
        Assert.NotEqual(Make(symbol: "HealAsync").Id, Make(symbol: "VerifyAsync").Id);

    [Fact]
    public void RelativePathUnderPackageRootIsForwardSlashedAndColonFree()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "pkg-root");
        var filePath = Path.Combine(packageRoot, "src", "DevP2P.Sync", "TrieHealer.cs");
        var relative = Path.GetRelativePath(packageRoot, filePath).Replace('\\', '/');

        var finding = Make() with { FilePath = relative };

        Assert.DoesNotContain(':', finding.FilePath);
        Assert.DoesNotContain('\\', finding.FilePath);
    }

    [Fact]
    public void GetRelativePathLeavesCrossRootPathsAbsolute()
    {
        var relative = Path.GetRelativePath(@"C:\a", @"D:\b\c.cs");

        Assert.StartsWith("D:", relative);
        Assert.Contains(':', relative);
    }
}
