using CodeQuality.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuality.Core.Rules;

public static class CommentedOutCodeRule
{
    // Rounds 1 and 2 both tried to answer "is this code?" with a regex over punctuation (a
    // terminating `;`, then `;` plus `(` or `=`) and both produced false `Delete`s on real prose —
    // a parenthetical aside, an equality statement, a URL query string, a backtick-quoted snippet
    // inside a sentence all satisfy those shapes without being code. The project already carries a
    // C# parser, so this asks it directly: `Delete` fires only when the text parses as C# with zero
    // errors AND yields a node that is unambiguously executable, not merely syntactically legal
    // (a bare identifier statement like `done;` parses cleanly but carries no code).
    //
    // A pathologically deep bracket nesting (`((((...`) drives the recursive-descent parser into an
    // uncatchable stack overflow that kills the process — proven by probe: depth 1000 parsed
    // cleanly, depth 1500 crashed. Depth, not length, is what overflows the parser — a long but
    // flat commented-out method is exactly the genuine `Delete` case, so length only guards
    // performance (10,000 is generous headroom, never hit by real comments) while depth guards the
    // crash itself, at 100, a 10x margin below the measured-safe 1000.
    const int MaxParsableLength = 10_000;
    const int MaxNestingDepth = 100;

    public static IEnumerable<Finding> Apply(RuleContext context, CommentBlock block)
    {
        if (block.Kind != CommentKind.Line) yield break;

        var trimmed = block.Text.Trim();
        if (trimmed.Length == 0) yield break;

        var classification = Classify(trimmed, block.Text);
        if (classification is { } found)
            yield return Make(context, block, found.RuleId, found.Verdict, found.Evidence);
    }

    readonly record struct Classification(string RuleId, Verdict Verdict, string Evidence);

    // Named so each branch of the original inline decision reads as what it is: a brace-only
    // line is always code, an unparsed-for-safety line and a parses-as-code line are both
    // `Delete`/`Review` calls on their own terms, and a semicolon-terminated line that failed to
    // parse is the last, weakest signal.
    static Classification? Classify(string trimmed, string rawText)
    {
        if (trimmed is "{" or "}")
            return new Classification("comment.commented-out-code", Verdict.Delete,
                ReferenceRules.Excerpt(rawText));

        // A comment the tool declines to parse must never look the same as one it parsed and
        // cleared — otherwise a skipped block is silently indistinguishable from safe prose, which
        // is the exact silent-success failure this rule exists to prevent.
        var skipReason = WhyUnsafeToParse(trimmed);
        if (skipReason is not null)
            return new Classification("comment.possible-commented-out-code", Verdict.Review,
                $"not parsed ({skipReason}) — judge manually: " + ReferenceRules.Excerpt(rawText));

        if (IsGenuineCode(trimmed))
            return new Classification("comment.commented-out-code", Verdict.Delete,
                ReferenceRules.Excerpt(rawText));

        if (trimmed.EndsWith(';'))
            return new Classification("comment.possible-commented-out-code", Verdict.Review,
                "ends in a semicolon but does not parse as executable code — judge manually: " + ReferenceRules.Excerpt(rawText));

        return null;
    }

    static string? WhyUnsafeToParse(string text)
    {
        if (text.Length > MaxParsableLength) return "too long to parse";

        return MaxBracketDepth(text) > MaxNestingDepth ? "too deeply nested to parse" : null;
    }

    static int MaxBracketDepth(string text)
    {
        var depth = 0;
        var maxDepth = 0;
        foreach (var c in text)
            TrackBracketDepth(c, ref depth, ref maxDepth);

        return maxDepth;
    }

    static void TrackBracketDepth(char c, ref int depth, ref int maxDepth)
    {
        if (c is '(' or '[' or '{')
        {
            depth++;
            if (depth > maxDepth) maxDepth = depth;
        }
        else if (c is ')' or ']' or '}')
        {
            if (depth > 0) depth--;
        }
    }

    static bool IsGenuineCode(string text)
    {
        var tree = CSharpSyntaxTree.ParseText(text);
        if (tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error)) return false;

        var root = tree.GetRoot();

        // A label reads as code ("retry:") but "Note: retries = 3;" and "Deprecated: Dispose();"
        // parse the same way — a prose lead-in followed by a colon in front of a statement that
        // WOULD independently qualify below. Vetoing the whole tree the moment any label appears,
        // rather than only excluding the LabeledStatementSyntax node itself, stops that inner
        // statement from being picked up on its own by the DescendantNodes scan.
        if (root.DescendantNodesAndSelf().Any(n => n is LabeledStatementSyntax)) return false;

        return root.DescendantNodes().Any(IsCodeBearing);
    }

    // A bare identifier expression statement (`done;`) parses without a single diagnostic — the
    // grammar allows it — but it is not code, so it is excluded on purpose by only accepting the
    // node shapes that actually do something: declare, direct control flow, call, or assign.
    static bool IsCodeBearing(SyntaxNode node) => node switch
    {
        // "Block number;" and "Sender address;" are two-word noun phrases that also happen to be
        // valid type-plus-identifier declarations with nothing assigned. A real commented-out
        // declaration almost always assigns something; one that does not is indistinguishable
        // from prose, so only the initialised form counts.
        LocalDeclarationStatementSyntax { Declaration.Variables: [{ Initializer: null }] } => false,
        LocalDeclarationStatementSyntax => true,
        // "using cached;" parses as a using-directive naming a bare, unqualified type — read as
        // prose it means "using the cached value". A genuine commented-out import is qualified
        // ("using System.Dynamic;"), so only the qualified form counts.
        UsingDirectiveSyntax { Name: IdentifierNameSyntax } => false,
        UsingDirectiveSyntax => true,
        IfStatementSyntax => true,
        ForStatementSyntax => true,
        ForEachStatementSyntax => true,
        WhileStatementSyntax => true,
        // "return early;" is a bare return-plus-word, the same ambiguity as the declaration case
        // above; "return someExpression();" or "return null;" are unaffected since neither is a
        // lone identifier.
        ReturnStatementSyntax { Expression: IdentifierNameSyntax } => false,
        ReturnStatementSyntax => true,
        ThrowStatementSyntax => true,
        // "e.g. Reset();" and "i.e. Dispose();" parse as an invocation on the member-access chain
        // e.g.Reset()/i.e.Dispose() — an abbreviation followed by a capitalised word, not a call
        // on an actual object.
        ExpressionStatementSyntax { Expression: InvocationExpressionSyntax { Expression: var callee } }
            when IsAbbreviationRooted(callee) => false,
        ExpressionStatementSyntax { Expression: InvocationExpressionSyntax or AssignmentExpressionSyntax } => true,
        _ => false,
    };

    static bool IsAbbreviationRooted(ExpressionSyntax expression)
    {
        var segments = new List<string>();
        var current = expression;
        while (current is MemberAccessExpressionSyntax member)
        {
            segments.Insert(0, member.Name.Identifier.Text);
            current = member.Expression;
        }

        if (current is not IdentifierNameSyntax root || segments.Count == 0) return false;
        segments.Insert(0, root.Identifier.Text);

        return (segments[0] == "e" || segments[0] == "i") && segments[1] == (segments[0] == "e" ? "g" : "e");
    }

    static Finding Make(RuleContext context, CommentBlock block, string ruleId, Verdict verdict, string evidence) =>
        new(
            Package: context.Package,
            FilePath: context.RelativePath,
            Line: block.StartLine,
            Symbol: block.EnclosingMethod ?? "<file>",
            Kind: FindingKind.Comment,
            RuleId: ruleId,
            Verdict: verdict,
            Evidence: evidence,
            Confidence: 1.0,
            Profile: context.ProfileName);
}
