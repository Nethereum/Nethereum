using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuality.Core.Semantic;

/// <summary>
/// The code an EIP actually reaches, derived from the syntax and semantic model rather than
/// from file names. Seeding on a name pattern and stopping there answers "which files did
/// someone call Eip8038" — a different and much smaller question than "what does EIP-8038 do
/// here", which is what an audit has to walk.
/// </summary>
public sealed class EipSurfaceWalker
{
    private readonly CSharpCompilation _compilation;
    private readonly Dictionary<ISymbol, HashSet<ISymbol>> _callees;
    private readonly Dictionary<ISymbol, HashSet<ISymbol>> _callers;

    private EipSurfaceWalker(CSharpCompilation compilation,
        Dictionary<ISymbol, HashSet<ISymbol>> callees,
        Dictionary<ISymbol, HashSet<ISymbol>> callers)
    {
        _compilation = compilation;
        _callees = callees;
        _callers = callers;
    }

    public static EipSurfaceWalker Over(IEnumerable<string> roots)
    {
        var trees = new List<SyntaxTree>();
        foreach (var root in roots)
            foreach (var file in EnumerateSources(root))
                trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file));

        var compilation = CSharpCompilation.Create("eip-surface", trees, PlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var callees = new Dictionary<ISymbol, HashSet<ISymbol>>(SymbolEqualityComparer.Default);
        var callers = new Dictionary<ISymbol, HashSet<ISymbol>>(SymbolEqualityComparer.Default);

        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var member in tree.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                var from = model.GetDeclaredSymbol(member);
                if (from is null) continue;

                foreach (var node in member.DescendantNodes())
                {
                    var to = ResolvedTarget(model, node);
                    if (to is null || !IsFromSource(to) || SymbolEqualityComparer.Default.Equals(to, from))
                        continue;
                    Link(callees, from, to);
                    Link(callers, to, from);
                }
            }
        }

        return new EipSurfaceWalker(compilation, callees, callers);
    }

    /// <summary>
    /// Without the framework assemblies an overload whose resolution touches a BCL type binds to
    /// nothing, so a call taking a <c>List&lt;T&gt;</c> silently vanishes from the graph while the
    /// argument-less call beside it survives. Calibration caught exactly that.
    /// </summary>
    private static IEnumerable<MetadataReference> PlatformReferences() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p));

    private static ISymbol? ResolvedTarget(SemanticModel model, SyntaxNode node) => node switch
    {
        InvocationExpressionSyntax or ObjectCreationExpressionSyntax
            => Bound(model, node),
        IdentifierNameSyntax or MemberAccessExpressionSyntax
            => Bound(model, node) is { } s
               && s.Kind is SymbolKind.Field or SymbolKind.Property or SymbolKind.NamedType ? s : null,
        _ => null,
    };

    /// <summary>
    /// A partial compilation cannot see every root, so a call whose parameter type is declared
    /// outside them fails overload resolution and <c>Symbol</c> is null even though exactly one
    /// candidate exists. Taking the sole candidate keeps such a call in the graph; two or more
    /// candidates stay unresolved, because guessing between them would invent an edge.
    /// </summary>
    private static ISymbol? Bound(SemanticModel model, SyntaxNode node)
    {
        var info = model.GetSymbolInfo(node);
        if (info.Symbol is not null) return info.Symbol;
        return info.CandidateSymbols.Length == 1 ? info.CandidateSymbols[0] : null;
    }

    private static bool IsFromSource(ISymbol symbol) =>
        symbol.DeclaringSyntaxReferences.Length > 0;

    private static void Link(Dictionary<ISymbol, HashSet<ISymbol>> map, ISymbol key, ISymbol value)
    {
        if (!map.TryGetValue(key, out var set))
            map[key] = set = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        set.Add(value);
    }

    /// <summary>
    /// Members that NAME the EIP — in an identifier or in a comment. This is the seed, not the
    /// answer: it is what a filename grep would find, and the walk below is what it misses.
    /// </summary>
    public IReadOnlyCollection<ISymbol> Seed(string eip)
    {
        var pattern = new Regex($@"\bEIP[-_ ]?{eip}\b|\bEip{eip}\b", RegexOptions.IgnoreCase);
        var seed = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var tree in _compilation.SyntaxTrees)
        {
            if (!pattern.IsMatch(tree.ToString())) continue;
            var model = _compilation.GetSemanticModel(tree);
            foreach (var member in tree.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                var text = member.ToFullString();
                if (!pattern.IsMatch(text)) continue;
                if (model.GetDeclaredSymbol(member) is { } symbol) seed.Add(symbol);
            }
        }
        return seed;
    }

    /// <summary>Everything reachable from the seed, following calls in both directions.</summary>
    public IReadOnlyCollection<ISymbol> Reachable(IEnumerable<ISymbol> seed, int depth)
    {
        var seen = new HashSet<ISymbol>(seed, SymbolEqualityComparer.Default);
        var frontier = new List<ISymbol>(seen);

        for (var step = 0; step < depth && frontier.Count > 0; step++)
        {
            var next = new List<ISymbol>();
            foreach (var symbol in frontier)
            {
                foreach (var map in new[] { _callees, _callers })
                {
                    if (!map.TryGetValue(symbol, out var linked)) continue;
                    foreach (var target in linked)
                        if (seen.Add(target)) next.Add(target);
                }
            }
            frontier = next;
        }
        return seen;
    }

    /// <summary>One resolved call, at the depth and source order it occurs.</summary>
    public sealed record SequenceStep(int Depth, ISymbol Symbol, string Where, bool Recursed);

    /// <summary>
    /// The algorithm a member runs, in the order it runs it: every resolved call in SOURCE
    /// order, recursing into members declared in these trees. This is what an ordering rule is
    /// validated against — "before any other logs", "clear, then the EIP-161 sweep" — because
    /// the order is read off the tree rather than inferred from names.
    ///
    /// <para>Branches are flattened. A step appearing here means the code reaches it on SOME
    /// path, not on every path, and a conditional is not shown as one. An auditor checking a
    /// rule that only binds on one branch must still open the source.</para>
    /// </summary>
    public IReadOnlyList<SequenceStep> Sequence(ISymbol entry, int depth) =>
        Sequence(entry, depth, out _);

    /// <param name="outsideRoots">
    /// Calls the entry makes to members declared outside the walked roots. A sequence of zero
    /// steps means "calls nothing IN THESE ROOTS", which is a different claim from "calls
    /// nothing" — and without this count the two are indistinguishable.
    /// </param>
    public IReadOnlyList<SequenceStep> Sequence(ISymbol entry, int depth, out int outsideRoots)
    {
        var steps = new List<SequenceStep>();
        outsideRoots = 0;
        Walk(entry, 0, depth, new HashSet<ISymbol>(SymbolEqualityComparer.Default), steps, ref outsideRoots);
        return steps;
    }

    private void Walk(ISymbol member, int level, int depth, HashSet<ISymbol> onPath,
        List<SequenceStep> steps, ref int outsideRoots)
    {
        if (level >= depth) return;
        var reference = member.DeclaringSyntaxReferences.FirstOrDefault();
        if (reference is null) return;

        var node = reference.GetSyntax();
        var model = _compilation.GetSemanticModel(node.SyntaxTree);

        // DescendantNodes is document order, which for a method body is execution order
        // modulo branching — the caveat the summary states.
        foreach (var child in node.DescendantNodes())
        {
            var target = ResolvedCall(model, child);
            if (target is null) continue;
            if (!IsFromSource(target))
            {
                if (level == 0) outsideRoots++;
                continue;
            }

            var cyclic = onPath.Contains(target);
            steps.Add(new SequenceStep(level, target, Where(target), !cyclic));
            if (cyclic) continue;

            onPath.Add(target);
            Walk(target, level + 1, depth, onPath, steps, ref outsideRoots);
            onPath.Remove(target);
        }
    }

    private static ISymbol? ResolvedCall(SemanticModel model, SyntaxNode node) => node switch
    {
        InvocationExpressionSyntax or ObjectCreationExpressionSyntax => Bound(model, node),
        _ => null,
    };

    public static IEnumerable<string> EnumerateSources(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var normalised = file.Replace('\\', '/');
            if (normalised.Contains("/obj/") || normalised.Contains("/bin/")) continue;
            yield return file;
        }
    }

    public static string Where(ISymbol symbol)
    {
        var reference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (reference is null) return "(no source)";
        var span = reference.SyntaxTree.GetLineSpan(reference.Span);
        return $"{reference.SyntaxTree.FilePath}:{span.StartLinePosition.Line + 1}";
    }

    public static int LineCount(ISymbol symbol)
    {
        var reference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (reference is null) return 0;
        var span = reference.SyntaxTree.GetLineSpan(reference.Span);
        return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
    }
}
