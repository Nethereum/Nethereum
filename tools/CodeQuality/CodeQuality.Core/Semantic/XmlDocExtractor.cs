using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeQuality.Core.Semantic
{
    /// <summary>
    /// Lifts /// blocks out of source and attaches each to the member it documents, read from
    /// the syntax tree rather than inferred from the line above. A block that lands on the
    /// wrong member is the defect this whole exercise exists to remove.
    /// </summary>
    public sealed class XmlDocExtractor
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
        private static bool CitesSpecification(string text) =>
            NamesASpec.IsMatch(text) || QuotedSentence.IsMatch(XmlTag.Replace(text, " "));

        public sealed record Doc(
            string File, string Namespace, string Type, string Member, string Kind,
            int Line, string Text, bool CitesSpec);

        public static IReadOnlyList<Doc> Extract(string path, string text)
        {
            var tree = CSharpSyntaxTree.ParseText(text, path: path);
            var root = tree.GetRoot();
            var docs = new List<Doc>();

            foreach (var member in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                var block = LeadingXmlDoc(member);
                if (block is null) continue;

                var span = tree.GetLineSpan(member.Span);
                docs.Add(new Doc(
                    File: path.Replace('\\', '/'),
                    Namespace: NamespaceOf(member),
                    Type: TypeOf(member),
                    Member: NameOf(member),
                    Kind: member.Kind().ToString().Replace("Declaration", string.Empty),
                    Line: span.StartLinePosition.Line + 1,
                    Text: block,
                    CitesSpec: CitesSpecification(block)));
            }
            return docs;
        }

        /// <summary>The block as written, with the /// markers and indentation stripped.</summary>
        private static string LeadingXmlDoc(MemberDeclarationSyntax member)
        {
            var lines = member.GetLeadingTrivia()
                .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                            || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                .Select(t => t.ToFullString())
                .ToList();
            if (lines.Count == 0) return null;

            var body = new StringBuilder();
            foreach (var raw in string.Concat(lines).Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.TrimStart();
                if (line.StartsWith("///")) line = line.Substring(3);
                if (line.Length > 0 && line[0] == ' ') line = line.Substring(1);
                body.AppendLine(line.TrimEnd());
            }
            return body.ToString().Trim('\n');
        }

        public static string StripXmlDocs(string text)
        {
            var root = CSharpSyntaxTree.ParseText(text).GetRoot();
            var rewritten = new XmlDocRemover().Visit(root);
            return rewritten.ToFullString();
        }

        private sealed class XmlDocRemover : CSharpSyntaxRewriter
        {
            public XmlDocRemover() : base(visitIntoStructuredTrivia: false) { }

            /// <summary>
            /// A doc block sits behind the whitespace that indents its first /// . Dropping only
            /// the doc trivia leaves that whitespace in front of the member, which then carries
            /// its own indentation on top and the member shifts right.
            /// </summary>
            public override SyntaxToken VisitToken(SyntaxToken token)
            {
                var leading = token.LeadingTrivia;
                if (!leading.Any(IsDoc)) return token;

                var kept = new List<SyntaxTrivia>();
                foreach (var trivia in leading)
                {
                    if (IsDoc(trivia))
                    {
                        if (kept.Count > 0 && kept[kept.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia))
                            kept.RemoveAt(kept.Count - 1);
                        continue;
                    }
                    kept.Add(trivia);
                }
                return token.WithLeadingTrivia(SyntaxFactory.TriviaList(kept));
            }

            private static bool IsDoc(SyntaxTrivia trivia) =>
                trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
        }

        private static string NamespaceOf(SyntaxNode node)
        {
            for (var n = node.Parent; n is not null; n = n.Parent)
            {
                if (n is BaseNamespaceDeclarationSyntax ns) return ns.Name.ToString();
            }
            return string.Empty;
        }

        private static string TypeOf(SyntaxNode node)
        {
            var names = new List<string>();
            for (var n = node.Parent; n is not null; n = n.Parent)
            {
                if (n is TypeDeclarationSyntax t) names.Insert(0, t.Identifier.Text);
            }
            return string.Join(".", names);
        }

        private static string NameOf(MemberDeclarationSyntax member) => member switch
        {
            MethodDeclarationSyntax m => m.Identifier.Text + ParameterList(m.ParameterList),
            ConstructorDeclarationSyntax c => c.Identifier.Text + ParameterList(c.ParameterList),
            PropertyDeclarationSyntax p => p.Identifier.Text,
            EventDeclarationSyntax e => e.Identifier.Text,
            IndexerDeclarationSyntax => "this[]",
            OperatorDeclarationSyntax o => "operator " + o.OperatorToken.Text,
            ConversionOperatorDeclarationSyntax => "operator",
            DelegateDeclarationSyntax d => d.Identifier.Text,
            EnumMemberDeclarationSyntax em => em.Identifier.Text,
            TypeDeclarationSyntax t => t.Identifier.Text,
            FieldDeclarationSyntax f => string.Join(", ",
                f.Declaration.Variables.Select(v => v.Identifier.Text)),
            EnumDeclarationSyntax e => e.Identifier.Text,
            _ => member.Kind().ToString(),
        };

        private static string ParameterList(BaseParameterListSyntax list) =>
            "(" + string.Join(", ", list.Parameters.Select(p => p.Type?.ToString() ?? "?")) + ")";

        public static IEnumerable<string> EnumerateSources(string root)
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var n = file.Replace('\\', '/');
                if (n.Contains("/obj/") || n.Contains("/bin/") || n.EndsWith(".gen.cs")) continue;
                yield return file;
            }
        }
    }
}
