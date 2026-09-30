using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuality.Core.Semantic
{
    /// <summary>
    /// Every comment in a file, attached to the member it sits on or inside, read from the
    /// syntax tree. A comment that cites a specification stays in the code; the rest is what
    /// this reports so it can be lifted out.
    /// </summary>
    public static class MemberCommentExtractor
    {
        private static readonly Regex NamesASpec =
            new Regex(@"EIP-\d{3,4}|ERC-\d{3,4}", RegexOptions.Compiled);

        private static readonly Regex XmlTag = new Regex("<[^>]*>", RegexOptions.Compiled);

        private static readonly Regex QuotedSentence =
            new Regex(@"""[^""<>]*\s+\S+\s+\S+\s+[^""<>]*""", RegexOptions.Compiled);

        /// <summary>
        /// A quote is a sentence someone quoted, not the gap between two XML attributes.
        /// Matching across a cref boundary - from the close of one see tag to the open of the
        /// next - kept 444 blocks that name no specification at all.
        /// </summary>
        /// <summary>
        /// A comment earns its place by QUOTING a specification, not by naming one. Keeping
        /// anything containing the token EIP-nnnn kept 1,770 blocks of prose that merely
        /// mention an EIP against 239 that actually quote one.
        /// </summary>
        private static bool CitesSpecification(string text)
        {
            var prose = XmlTag.Replace(text, " ");
            return QuotedSentence.IsMatch(prose) && NamesASpec.IsMatch(text);
        }

        public sealed record Inner(int Line, string Text, bool CitesSpec);

        public sealed record Member(
            string Type, string Name, string Kind, int Line,
            string Summary, bool SummaryCitesSpec, IReadOnlyList<Inner> Inside);

        /// <summary>
        /// Both preprocessor arms, merged. A member declared only under #if EVM_SYNC is
        /// invisible to the default parse, and so are its comments.
        /// </summary>
        public static IReadOnlyList<Member> Extract(string path, string text)
        {
            var seen = new HashSet<string>();
            var all = new List<Member>();
            foreach (var symbols in new[] { new string[0], new[] { "EVM_SYNC" } })
            {
                foreach (var member in ExtractOneArm(path, text, symbols))
                {
                    if (seen.Add($"{member.Type}|{member.Name}|{member.Line}"))
                        all.Add(member);
                }
            }
            return all.OrderBy(m => m.Line).ToList();
        }

        private static IReadOnlyList<Member> ExtractOneArm(string path, string text, string[] symbols)
        {
            var tree = CSharpSyntaxTree.ParseText(
                text, CSharpParseOptions.Default.WithPreprocessorSymbols(symbols), path: path);
            var root = tree.GetRoot();
            var members = new List<Member>();

            foreach (var node in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                var summary = LeadingDoc(node);
                var inside = InnerComments(node, tree);
                if (summary is null && inside.Count == 0) continue;

                members.Add(new Member(
                    Type: TypeOf(node),
                    Name: NameOf(node),
                    Kind: node.Kind().ToString().Replace("Declaration", string.Empty),
                    Line: tree.GetLineSpan(node.Span).StartLinePosition.Line + 1,
                    Summary: summary,
                    SummaryCitesSpec: summary is not null && CitesSpecification(summary),
                    Inside: inside));
            }
            return members;
        }

        /// <summary>
        /// Comments written INSIDE this member, excluding any that belong to a member nested
        /// within it - those are reported against the nested member instead.
        /// </summary>
        private static IReadOnlyList<Inner> InnerComments(MemberDeclarationSyntax node, SyntaxTree tree)
        {
            var nested = node.DescendantNodes()
                .OfType<MemberDeclarationSyntax>()
                .Where(n => n != node)
                .ToList();

            var found = new List<Inner>();
            foreach (var trivia in node.DescendantTrivia())
            {
                if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                    && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
                    continue;
                if (nested.Any(n => n.FullSpan.Contains(trivia.SpanStart)))
                    continue;

                var body = trivia.ToString().TrimStart('/', '*', ' ').TrimEnd('*', '/').Trim();
                if (body.Length == 0) continue;

                found.Add(new Inner(
                    tree.GetLineSpan(trivia.Span).StartLinePosition.Line + 1,
                    body,
                    CitesSpecification(trivia.ToString())));
            }
            return found;
        }

        private static string LeadingDoc(MemberDeclarationSyntax member)
        {
            var raw = member.GetLeadingTrivia()
                .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                            || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                .Select(t => t.ToFullString())
                .ToList();
            if (raw.Count == 0) return null;

            var body = new StringBuilder();
            foreach (var line in string.Concat(raw).Replace("\r\n", "\n").Split('\n'))
            {
                var s = line.TrimStart();
                if (s.StartsWith("///")) s = s.Substring(3);
                if (s.Length > 0 && s[0] == ' ') s = s.Substring(1);
                body.AppendLine(s.TrimEnd());
            }
            return body.ToString().Trim('\n');
        }

        /// <summary>Removes every comment that does not cite a specification.</summary>
        /// <summary>
        /// Run once per preprocessor arm. A file with #if EVM_SYNC has two bodies, and the one
        /// the default parse leaves inactive is disabled TEXT rather than comment trivia, so a
        /// single pass cannot see its comments at all.
        /// </summary>
        public static string StripUncitedComments(string text)
        {
            foreach (var symbols in new[] { new string[0], new[] { "EVM_SYNC" } })
            {
                var options = CSharpParseOptions.Default.WithPreprocessorSymbols(symbols);
                var root = CSharpSyntaxTree.ParseText(text, options).GetRoot();
                text = new UncitedCommentRemover().Visit(root).ToFullString();
            }
            return text;
        }

        private sealed class UncitedCommentRemover : CSharpSyntaxRewriter
        {
            public UncitedCommentRemover() : base(visitIntoStructuredTrivia: false) { }

            public override SyntaxToken VisitToken(SyntaxToken token)
            {
                var leading = Filter(token.LeadingTrivia, dropsWholeLine: true);
                var trailing = Filter(token.TrailingTrivia, dropsWholeLine: false);
                if (leading is null && trailing is null) return token;

                return token
                    .WithLeadingTrivia(leading ?? token.LeadingTrivia)
                    .WithTrailingTrivia(trailing ?? token.TrailingTrivia);
            }

            /// <summary>
            /// A comment on its own line takes its indentation and its newline with it, or the
            /// file gains a blank line where every comment used to be. A comment after code
            /// takes only the space before it.
            /// </summary>
            private static SyntaxTriviaList? Filter(SyntaxTriviaList list, bool dropsWholeLine)
            {
                if (!list.Any(IsComment)) return null;

                // Decide a contiguous run together. Testing each line on its own kept the lines
                // that happened to name an EIP and dropped the sentence around them, leaving
                // fragments that read as wreckage.
                var runCites = RunCitations(list);

                var kept = new List<SyntaxTrivia>();
                for (var i = 0; i < list.Count; i++)
                {
                    var trivia = list[i];
                    if (!IsComment(trivia) || runCites[i])
                    {
                        kept.Add(trivia);
                        continue;
                    }

                    var ownsTheLine = dropsWholeLine
                                      && (kept.Count == 0
                                          || kept[kept.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
                                          || kept[kept.Count - 1].IsKind(SyntaxKind.EndOfLineTrivia));

                    if (kept.Count > 0 && kept[kept.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia))
                        kept.RemoveAt(kept.Count - 1);

                    if (ownsTheLine && i + 1 < list.Count && list[i + 1].IsKind(SyntaxKind.EndOfLineTrivia))
                        i++;
                }
                return SyntaxFactory.TriviaList(kept);
            }

            /// <summary>
            /// For each trivia, whether the RUN it belongs to cites a specification. A run is a
            /// contiguous stretch of comments broken only by whitespace and single newlines.
            /// </summary>
            private static bool[] RunCitations(SyntaxTriviaList list)
            {
                var cites = new bool[list.Count];
                var i = 0;
                while (i < list.Count)
                {
                    if (!IsComment(list[i])) { i++; continue; }

                    var start = i;
                    var text = new StringBuilder();
                    var newlines = 0;
                    while (i < list.Count)
                    {
                        if (IsComment(list[i])) { text.Append(list[i].ToFullString()); newlines = 0; }
                        else if (list[i].IsKind(SyntaxKind.WhitespaceTrivia)) { }
                        else if (list[i].IsKind(SyntaxKind.EndOfLineTrivia))
                        {
                            if (++newlines > 1) break;
                        }
                        else break;
                        i++;
                    }
                    var cited = CitesSpecification(WithoutMarkers(text.ToString()));
                    for (var k = start; k < i && k < cites.Length; k++) cites[k] = cited;
                }
                return cites;
            }

            private static bool IsComment(SyntaxTrivia trivia) =>
                trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);

            private static bool Removable(SyntaxTrivia trivia)
            {
                var doc = trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                          || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
                var line = trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                           || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);
                if (!doc && !line) return false;
                return !CitesSpecification(WithoutMarkers(trivia.ToFullString()));
            }

            /// <summary>
            /// The extractor tests the text with its /// markers already gone. Testing the raw
            /// trivia instead gives the same block a different answer, and the tool then reports
            /// work it will not do.
            /// </summary>
            private static string WithoutMarkers(string text)
            {
                var body = new StringBuilder();
                foreach (var line in text.Split('\n'))
                {
                    var s = line.Trim('\r').TrimStart();
                    if (s.StartsWith("///")) s = s.Substring(3);
                    else if (s.StartsWith("//")) s = s.Substring(2);
                    body.AppendLine(s.Trim());
                }
                return body.ToString();
            }
        }

        private static string TypeOf(SyntaxNode node)
        {
            var names = new List<string>();
            for (var n = node.Parent; n is not null; n = n.Parent)
                if (n is TypeDeclarationSyntax t) names.Insert(0, t.Identifier.Text);
            return string.Join(".", names);
        }

        private static string NameOf(MemberDeclarationSyntax member) => member switch
        {
            MethodDeclarationSyntax m => m.Identifier.Text + Parameters(m.ParameterList),
            ConstructorDeclarationSyntax c => c.Identifier.Text + Parameters(c.ParameterList),
            PropertyDeclarationSyntax p => p.Identifier.Text,
            EventDeclarationSyntax e => e.Identifier.Text,
            IndexerDeclarationSyntax => "this[]",
            OperatorDeclarationSyntax o => "operator " + o.OperatorToken.Text,
            DelegateDeclarationSyntax d => d.Identifier.Text,
            EnumMemberDeclarationSyntax em => em.Identifier.Text,
            EnumDeclarationSyntax e => e.Identifier.Text,
            TypeDeclarationSyntax t => t.Identifier.Text,
            FieldDeclarationSyntax f => string.Join(", ", f.Declaration.Variables.Select(v => v.Identifier.Text)),
            _ => member.Kind().ToString(),
        };

        private static string Parameters(BaseParameterListSyntax list) =>
            "(" + string.Join(", ", list.Parameters.Select(p => p.Type?.ToString() ?? "?")) + ")";
    }
}
