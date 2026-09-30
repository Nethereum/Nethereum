using System.Text;
using CodeQuality.Core.Semantic;

namespace CodeQuality.Cli;

public static class CommentsCommand
{
    public static int Run(string sourceRoot, string outRoot, string repoRoot, bool apply)
    {
        if (!Directory.Exists(sourceRoot))
        {
            Console.Error.WriteLine($"not a directory: {sourceRoot}");
            return 2;
        }

        int files = 0, summaries = 0, inner = 0, keptCiting = 0, rewritten = 0;

        foreach (var file in XmlDocExtractor.EnumerateSources(sourceRoot))
        {
            var text = File.ReadAllText(file);

            // The report is derived from what the rewriter actually does. Deciding movability
            // separately let the two disagree, and the tool then announced work it would not do.
            var without = MemberCommentExtractor.StripUncitedComments(text);
            if (without == text) continue;

            var members = MemberCommentExtractor.Extract(file, text);

            var moving = members
                .Select(m => m with
                {
                    Summary = m.SummaryCitesSpec ? null : m.Summary,
                    Inside = m.Inside.Where(i => !i.CitesSpec).ToList(),
                })
                .Where(m => m.Summary is not null || m.Inside.Count > 0)
                .ToList();

            keptCiting += members.Count(m => m.SummaryCitesSpec)
                          + members.Sum(m => m.Inside.Count(i => i.CitesSpec));

            if (moving.Count == 0) continue;

            files++;
            summaries += moving.Count(m => m.Summary is not null);
            inner += moving.Sum(m => m.Inside.Count);

            if (!apply) continue;

            var relative = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            var target = Path.Combine(outRoot, Path.ChangeExtension(relative, ".md"));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, Render(relative, moving));

            File.WriteAllText(file, without, EncodingOf(file));
            rewritten++;
        }

        var verb = apply ? "moved" : "movable";
        Console.WriteLine($"{sourceRoot}");
        Console.WriteLine($"  {summaries:N0} summaries + {inner:N0} inner comments {verb}, from {files:N0} files");
        Console.WriteLine($"  {keptCiting:N0} comments cite a spec and stay in the code");
        if (apply) Console.WriteLine($"  {rewritten:N0} source files rewritten");
        return 0;
    }

    /// <summary>
    /// Consecutive comment lines are one paragraph the author wrote, not one finding each.
    /// Splitting them per line makes the archive unreadable.
    /// </summary>
    private static IEnumerable<List<MemberCommentExtractor.Inner>> Runs(
        IReadOnlyList<MemberCommentExtractor.Inner> comments)
    {
        var run = new List<MemberCommentExtractor.Inner>();
        foreach (var comment in comments)
        {
            if (run.Count > 0 && comment.Line != run[run.Count - 1].Line + 1)
            {
                yield return run;
                run = new List<MemberCommentExtractor.Inner>();
            }
            run.Add(comment);
        }
        if (run.Count > 0) yield return run;
    }

    private static Encoding EncodingOf(string file)
    {
        var head = new byte[3];
        using var stream = File.OpenRead(file);
        var read = stream.Read(head, 0, 3);
        var bom = read == 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF;
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: bom);
    }

    private static string Render(string relative, IReadOnlyList<MemberCommentExtractor.Member> members)
    {
        var page = new StringBuilder();
        page.AppendLine($"# {relative}");
        page.AppendLine();

        foreach (var group in members.GroupBy(m => m.Type))
        {
            page.AppendLine($"## {(string.IsNullOrEmpty(group.Key) ? "(file scope)" : group.Key)}");
            page.AppendLine();

            foreach (var member in group)
            {
                page.AppendLine($"### {member.Name}");
                page.AppendLine();
                page.AppendLine($"`{member.Kind}` · `{relative}:{member.Line}`");
                page.AppendLine();

                if (member.Summary is not null)
                {
                    page.AppendLine(member.Summary);
                    page.AppendLine();
                }

                foreach (var run in Runs(member.Inside))
                {
                    page.AppendLine($"`:{run.First().Line}`");
                    page.AppendLine();
                    page.AppendLine(string.Join(" ", run.Select(c => c.Text)));
                    page.AppendLine();
                }
            }
        }
        return page.ToString();
    }
}
