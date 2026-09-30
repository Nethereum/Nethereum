using System.Text.Json;
using CodeQuality.Core.Semantic;
using Microsoft.CodeAnalysis;

namespace CodeQuality.Cli;

/// <summary>
/// What an EIP's implementation actually reaches, walked through the semantic model.
/// The seed is what a name search finds; the surface is what the audit has to read.
/// </summary>
public static class EipCommand
{
    public static int Run(string eip, IReadOnlyList<string> roots, int depth, bool json,
        string? sequenceOf = null, bool flaggedOnly = false)
    {
        var walker = EipSurfaceWalker.Over(roots);
        var seed = walker.Seed(eip);
        if (seed.Count == 0)
        {
            Console.Error.WriteLine($"no member names EIP-{eip} in the given roots");
            return 1;
        }

        if (sequenceOf is not null)
            return Sequence(walker, eip, seed, sequenceOf, depth);

        var surface = walker.Reachable(seed, depth);
        var seedSet = new HashSet<ISymbol>(seed, SymbolEqualityComparer.Default);

        var rows = surface
            .Where(s => s.Kind is SymbolKind.Method or SymbolKind.NamedType or SymbolKind.Property)
            .Select(s => new
            {
                Symbol = s.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                Kind = s.Kind.ToString(),
                Where = EipSurfaceWalker.Where(s),
                Lines = EipSurfaceWalker.LineCount(s),
                Seeded = seedSet.Contains(s),
            })
            .OrderByDescending(r => r.Lines)
            .ToList();

        var files = rows.Select(r => r.Where.Split(':')[0]).Distinct().Count();

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Eip = eip,
                Depth = depth,
                SeedMembers = seed.Count,
                SurfaceMembers = rows.Count,
                Files = files,
                Members = rows,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.WriteLine($"EIP-{eip}  seed {seed.Count} members  ->  surface {rows.Count} members across {files} files  (depth {depth})");
        Console.WriteLine();
        Console.WriteLine($"  {"lines",5}  {"seed",-4}  {"kind",-9}  {"member",-58}  where");
        Console.WriteLine("  " + new string('-', 118));
        foreach (var row in rows.Take(60))
        {
            var where = row.Where.Replace('\\', '/');
            var tail = where.Length > 44 ? where[^44..] : where;
            Console.WriteLine($"  {row.Lines,5}  {(row.Seeded ? "*" : ""),-4}  {row.Kind,-9}  {Trim(row.Symbol, 58),-58}  {tail}");
        }
        if (rows.Count > 60) Console.WriteLine($"  ... {rows.Count - 60} more");
        return 0;
    }

    /// <summary>
    /// The algorithm, read off the tree. An auditor validating an ORDERING rule reads this
    /// rather than inferring the order from method names.
    /// </summary>
    private static int Sequence(EipSurfaceWalker walker, string eip,
        IReadOnlyCollection<ISymbol> seed, string entryName, int depth)
    {
        var candidates = seed
            .Concat(walker.Reachable(seed, 1))
            .Where(s => s.Kind == SymbolKind.Method)
            .Where(s => s.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                         .Contains(entryName, StringComparison.OrdinalIgnoreCase))
            .Distinct(SymbolEqualityComparer.Default)
            .Cast<ISymbol>()
            .ToList();

        if (candidates.Count == 0)
        {
            Console.Error.WriteLine($"no method matching '{entryName}' in EIP-{eip}'s surface");
            return 1;
        }
        if (candidates.Count > 1)
        {
            Console.Error.WriteLine($"'{entryName}' matches {candidates.Count} methods — narrow it:");
            foreach (var c in candidates.Take(12))
                Console.Error.WriteLine($"   {c.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}");
            return 2;
        }

        var entry = candidates[0];
        var steps = walker.Sequence(entry, depth, out var outsideRoots);

        Console.WriteLine($"EIP-{eip} — sequence of {entry.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}");
        Console.WriteLine($"  {EipSurfaceWalker.Where(entry).Replace('\\', '/')}");
        Console.WriteLine();
        Console.WriteLine("  Source order, branches flattened: a step is reached on SOME path, not every path.");
        Console.WriteLine();
        foreach (var step in steps)
        {
            var name = step.Symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            var mark = step.Recursed ? " " : "↺";
            Console.WriteLine($"  {new string(' ', step.Depth * 2)}{mark} {Trim(name, 96 - step.Depth * 2)}");
        }
        Console.WriteLine();
        Console.WriteLine($"  {steps.Count} steps, depth {depth}"
            + (outsideRoots > 0 ? $"  (+{outsideRoots} direct calls outside the walked roots)" : ""));
        return 0;
    }

    private static string Trim(string value, int width) =>
        value.Length <= width ? value : value[..(width - 1)] + "…";
}
