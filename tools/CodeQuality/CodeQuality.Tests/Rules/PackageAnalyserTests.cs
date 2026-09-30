using CodeQuality.Core.Analysis;
using CodeQuality.Core.Config;
using CodeQuality.Core.Model;
using CodeQuality.Core.Rules;
using Xunit;

namespace CodeQuality.Tests.Rules;

public class PackageAnalyserTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "cq-" + Guid.NewGuid().ToString("N"));

    public PackageAnalyserTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    void Write(string name, string content) => File.WriteAllText(Path.Combine(_root, name), content);

    static CodeQualityConfig Config() => ConfigLoader.LoadFrom("""
        generated:
          fileNameSuffixes: [".gen.cs"]
        profiles:
          default:
            maxMethodLines: 5
        """);

    const string LongMethod = """
        public class Thing
        {
            public void Do()
            {
                var a = 1;
                var b = 2;
                var c = 3;
                var d = 4;
                var e = 5;
                var f = 6;
            }
        }
        """;

    [Fact]
    public void ReportsFindingsForHandWrittenFiles()
    {
        Write("Thing.cs", LongMethod);

        var result = new PackageAnalyser(Config()).Analyse(_root);

        Assert.Equal(1, result.FilesAnalysed);
        Assert.Contains(result.Findings, f => f.RuleId == "structure.long-method");
    }

    [Fact]
    public void ExcludesGeneratedFilesAndReportsTheCount()
    {
        Write("Thing.cs", LongMethod);
        Write("Other.gen.cs", LongMethod);

        var result = new PackageAnalyser(Config()).Analyse(_root);

        Assert.Equal(1, result.FilesAnalysed);
        Assert.Equal(1, result.FilesExcluded);
        Assert.Equal(0, result.FilesUnreadable);
        Assert.DoesNotContain(result.Findings, f => f.FilePath.Contains("Other.gen.cs"));
    }

    [Fact]
    public void DetectsClonesAcrossFilesInThePackage()
    {
        var body = string.Join("\n", Enumerable.Range(0, 9).Select(i => $"        var v{i} = {i};"));
        Write("A.cs", $"public class A {{ public void Run() {{\n{body}\n}} }}");
        Write("B.cs", $"public class B {{ public void Run() {{\n{body}\n}} }}");

        var result = new PackageAnalyser(Config()).Analyse(_root);

        var group = Assert.Single(result.Clones);
        Assert.Equal(2, group.Members.Count);
        Assert.Contains(result.Findings, f => f.RuleId == "clone.duplicate-method");
    }

    [Fact]
    public void EmptyDirectoryProducesAnEmptyResultNotACrash()
    {
        var result = new PackageAnalyser(Config()).Analyse(_root);

        Assert.Equal(0, result.FilesAnalysed);
        Assert.Empty(result.Findings);
        Assert.Empty(result.Clones);
    }

    [Fact]
    public void FileThatDoesNotCompileIsStillAnalysed()
    {
        Write("Broken.cs", "public class Broken { public void Do() { var a = Missing.Type.Call(; } }");

        var result = new PackageAnalyser(Config()).Analyse(_root);

        Assert.Equal(1, result.FilesAnalysed);
    }

    [Fact]
    public void SkipsBinAndObjDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_root, "obj"));
        File.WriteAllText(Path.Combine(_root, "obj", "Temp.cs"), LongMethod);

        var result = new PackageAnalyser(Config()).Analyse(_root);

        Assert.Equal(0, result.FilesAnalysed);
    }

    sealed class StubAnalyser : IAnalyser
    {
        public string Id => "stub";
        public IReadOnlyList<Finding> Analyse(PackageModel model, RuleContext context) =>
            new[] { new Finding(model.Package, "x.cs", 1, "S", FindingKind.Structure,
                "stub.rule", Verdict.Review, $"{model.Files.Count} files", 1.0, context.ProfileName) };
    }

    [Fact]
    public void HostRunsWhicheverAnalysersItIsGiven()
    {
        Write("Thing.cs", LongMethod);

        var result = new PackageAnalyser(Config(), new IAnalyser[] { new StubAnalyser() }).Analyse(_root);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("stub.rule", finding.RuleId);
        Assert.Equal("1 files", finding.Evidence);
    }

    [Fact]
    public void PackageIsParsedOnceAndSharedWithEveryAnalyser()
    {
        Write("Thing.cs", LongMethod);
        var first = new StubAnalyser();
        var second = new StubAnalyser();

        var result = new PackageAnalyser(Config(), new IAnalyser[] { first, second }).Analyse(_root);

        Assert.Equal(2, result.Findings.Count);
    }

    // Ground truth for this test was hand-computed from the probe in the task's design notes: an
    // `#if` guarding a symbol that is never defined in config.Parse.PreprocessorSymbols parses as
    // DisabledTextTrivia, so its method is invisible to every analyser. The count is what tells a
    // reader the package was not read in full, rather than reporting a clean package it never saw.
    // This would fail if FilesWithDisabledRegions were hardcoded to 0, or if the disabled file were
    // silently dropped instead of counted.
    [Fact]
    public void CountsFilesWithInactivePreprocessorRegionsAndStillAnalysesTheVisibleCode()
    {
        Write("Gated.cs", """
            public class Gated
            {
            #if NEVER_DEFINED
                public void Ghost() { var a = 1; }
            #endif
                public void Real() { }
            }
            """);

        var result = new PackageAnalyser(Config()).Analyse(_root);

        Assert.Equal(1, result.FilesAnalysed);
        Assert.Equal(1, result.FilesWithDisabledRegions);
        Assert.Equal(1, result.MethodCount);
    }

    // Ground truth: config.Parse.PreprocessorSymbols is set by hand to the exact symbol the file
    // guards on, so the region becomes active and its method must be visible. This is the inverse
    // of the previous test and would fail if the host ignored PreprocessorSymbols entirely (e.g.
    // parsed with default options rather than the configured ones).
    [Fact]
    public void ActivatingTheConfiguredSymbolMakesTheGuardedMethodVisible()
    {
        Write("Gated.cs", """
            public class Gated
            {
            #if FEATURE_X
                public void Ghost() { var a = 1; }
            #endif
                public void Real() { }
            }
            """);
        var config = ConfigLoader.LoadFrom("""
            parse:
              preprocessorSymbols: ["FEATURE_X"]
            profiles:
              default:
                maxMethodLines: 5
            """);

        var result = new PackageAnalyser(config).Analyse(_root);

        Assert.Equal(0, result.FilesWithDisabledRegions);
        Assert.Equal(2, result.MethodCount);
    }

    // Ground truth: a binding naming a profile absent from `profiles:` is now rejected as a
    // configuration error at ConfigLoader.LoadFrom itself (see ConfigLoaderTests), so a
    // PackageAnalyser can never be constructed with one - it is not reachable here to degrade
    // anything. This test instead pins the positive case: a binding naming a profile that DOES
    // exist is honoured, applying that profile's (stricter) threshold rather than default's.
    [Fact]
    public void BindingNamingAnExistingProfileAppliesThatProfilesThresholds()
    {
        Write("Thing.cs", LongMethod);
        var config = ConfigLoader.LoadFrom("""
            bindings:
              - glob: "**"
                profile: strict
            profiles:
              strict:
                maxMethodLines: 5
              default:
                maxMethodLines: 100
            """);

        var result = new PackageAnalyser(config).Analyse(_root);

        Assert.Equal("strict", result.ProfileName);
        Assert.Contains(result.Findings, f => f.RuleId == "structure.long-method");
    }

    // Ground truth: hand-picked path that does not exist on disk. Directory.EnumerateFiles throws
    // DirectoryNotFoundException for a missing root; this test would fail (via exception) if that
    // guard were removed from EnumerateSourceFiles.
    [Fact]
    public void MissingPackageDirectoryDegradesToAnEmptyResultNotACrash()
    {
        var missing = Path.Combine(_root, "does-not-exist");

        var result = new PackageAnalyser(Config()).Analyse(missing);

        Assert.Equal(0, result.FilesAnalysed);
        Assert.Empty(result.Findings);
    }

    // Ground truth: the file is opened for exclusive access by this test process itself before
    // Analyse runs, so File.ReadAllText inside the host is guaranteed to fail with IOException on
    // Windows. This would fail (via unhandled exception propagating out of Analyse) if the
    // try/catch around the read were removed.
    [Fact]
    public void LockedFileIsCountedAsUnreadableNotExcludedAndDoesNotCrashTheRun()
    {
        Write("Thing.cs", LongMethod);
        var locked = Path.Combine(_root, "Locked.cs");
        File.WriteAllText(locked, LongMethod);

        using var handle = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);
        var result = new PackageAnalyser(Config()).Analyse(_root);

        Assert.Equal(1, result.FilesAnalysed);
        Assert.Equal(1, result.FilesUnreadable);
        Assert.Equal(0, result.FilesExcluded);
    }
}
