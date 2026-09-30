using CodeQuality.Core.Config;
using CodeQuality.Core.Exclusion;
using CodeQuality.Core.Model;
using CodeQuality.Core.Parsing;
using CodeQuality.Core.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeQuality.Core.Analysis;

public sealed class PackageAnalyser
{
    static readonly string[] SkippedDirectories = { "bin", "obj", ".git" };

    readonly CodeQualityConfig _config;
    readonly GeneratedFileFilter _filter;
    readonly IReadOnlyList<IAnalyser> _analysers;

    public PackageAnalyser(CodeQualityConfig config, IReadOnlyList<IAnalyser>? analysers = null)
    {
        _config = config;
        _filter = new GeneratedFileFilter(config.Generated);
        _analysers = analysers ?? BuiltIn(config);
    }

    static IReadOnlyList<IAnalyser> BuiltIn(CodeQualityConfig config) => new IAnalyser[]
    {
        new StructureAnalyser(),
        new CommentAnalyser(),
        new CloneAnalyser(config.Clones.MinStatements),
    };

    public AnalysisResult Analyse(string packageDirectory)
    {
        // ConfigLoader.ValidateBindings already rejected any binding naming an undeclared
        // profile, and always adds a "default" entry, so this lookup cannot fail for a config
        // that reached this point. It is resolved BEFORE the model is built because the type
        // metrics count how many of a type's methods exceed the profile's method-length limit,
        // and that limit is a property of the profile bound to this package.
        var profileName = ProfileResolver.Resolve(_config, packageDirectory);
        var profile = _config.Profiles[profileName];

        var model = BuildModel(packageDirectory, profile.MaxMethodLines);
        var context = new RuleContext(model.Package, string.Empty, profileName, profile, _config);

        var findings = _analysers.SelectMany(a => a.Analyse(model, context)).ToList();
        var clones = _analysers.OfType<CloneAnalyser>().SelectMany(a => a.LastGroups).ToList();
        var methods = model.AllMethods.ToList();

        return new AnalysisResult(
            Package: model.Package,
            ProfileName: profileName,
            ConfigSourcePath: _config.SourcePath,
            ReferenceChecksInert: _config.References.Vendors.Count == 0,
            FilesAnalysed: model.Files.Count,
            FilesExcluded: model.FilesExcluded,
            FilesUnreadable: model.FilesUnreadable,
            FilesWithDisabledRegions: model.FilesWithDisabledRegions,
            MethodCount: methods.Count,
            CommentBlockCount: model.AllComments.Count(),
            Methods: methods,
            Findings: findings,
            Clones: clones,
            TypeCount: model.Types.Count,
            Types: model.Types);
    }

    PackageModel BuildModel(string packageDirectory, int maxMethodLines)
    {
        var files = new List<SourceFile>();
        var excluded = 0;
        var unreadable = 0;
        var withDisabledRegions = 0;

        // Ruling P16: parse once per file with the configured symbols, and hand the same root to
        // every parser. Re-parsing per method was measurable; more importantly, a file parsed with
        // different options twice could disagree with itself.
        var options = new CSharpParseOptions(preprocessorSymbols: _config.Parse.PreprocessorSymbols);

        foreach (var path in EnumerateSourceFiles(packageDirectory))
        {
            var scanned = ScanFile(path, packageDirectory, options, maxMethodLines);
            if (scanned.Unreadable) { unreadable++; continue; }
            if (scanned.Excluded) { excluded++; continue; }
            if (scanned.HasDisabledRegions) withDisabledRegions++;
            files.Add(scanned.File!);
        }

        var types = TypeWalker.Merge(files.SelectMany(f => f.TypeParts), maxMethodLines);

        return new PackageModel(new DirectoryInfo(packageDirectory).Name, packageDirectory,
            files, types, excluded, unreadable, withDisabledRegions);
    }

    readonly record struct FileScan(bool Unreadable, bool Excluded, bool HasDisabledRegions, SourceFile? File);

    FileScan ScanFile(string path, string packageDirectory, CSharpParseOptions options,
        int maxMethodLines)
    {
        // A file the OS refuses to hand over (locked by another process, permission denied)
        // must not abort the whole package, but it is a FAILURE to analyse a file the tool was
        // supposed to read — a decision the tool never got to make — not a deliberate exclusion
        // like a generated-file match. Counting it as "excluded" would tell the reader the tool
        // looked at the file and chose to skip it, when in truth it never saw the contents at
        // all, so it gets its own count instead.
        if (TryReadFile(path) is not { } text)
            return new FileScan(Unreadable: true, Excluded: false, HasDisabledRegions: false, File: null);

        if (_filter.IsGenerated(path, text))
            return new FileScan(Unreadable: false, Excluded: true, HasDisabledRegions: false, File: null);

        var relative = Relative(packageDirectory, path);
        var root = CSharpSyntaxTree.ParseText(text, options).GetRoot();

        var sourceFile = new SourceFile(
            RelativePath: relative,
            Text: text,
            Root: root,
            Methods: SourceFileParser.ParseMethods(relative, root),
            TypeParts: TypeWalker.Collect(relative, root, maxMethodLines),
            Comments: SourceFileParser.ParseComments(relative, root));

        return new FileScan(Unreadable: false, Excluded: false,
            HasDisabledRegions: HasDisabledRegions(root), File: sourceFile);
    }

    static string? TryReadFile(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Ruling P16: code inside an inactive `#if` is parsed as disabled text — its methods and
    // comments never reach any analyser. Reporting zero findings for code that was never read
    // is the silent-success failure this tool exists to avoid, so the count is surfaced.
    static bool HasDisabledRegions(SyntaxNode root) =>
        root.DescendantTrivia().Any(t => t.IsKind(SyntaxKind.DisabledTextTrivia));

    static IEnumerable<string> EnumerateSourceFiles(string root) =>
        Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(segment => SkippedDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase)))
                .OrderBy(f => f, StringComparer.Ordinal)
            : Enumerable.Empty<string>();

    // Ruling P19: Path.GetRelativePath returns its input UNCHANGED — drive letter included — when
    // the two paths sit on different roots, rather than throwing or signalling failure. `FilePath`
    // feeds directly into `Finding.Id`, a SQLite primary key, so silently accepting an absolute
    // path here would put a drive colon and backslashes into the id. Every path this method sees
    // comes from `EnumerateSourceFiles(root)`, so it is always under `root`; the check below is a
    // guard against that invariant ever being violated by a future caller, not a case reachable
    // through the file system today.
    static string Relative(string root, string file)
    {
        var relative = Path.GetRelativePath(root, file);
        if (Path.IsPathRooted(relative))
            throw new InvalidOperationException(
                $"'{file}' is not under package root '{root}'; refusing to use an absolute path as a finding id component.");

        return relative.Replace('\\', '/');
    }
}
