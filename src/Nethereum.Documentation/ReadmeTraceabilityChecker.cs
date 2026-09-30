using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Nethereum.Documentation
{
    public class ReadmeTraceabilityChecker
    {
        private readonly DocSection _section;
        private readonly Assembly _assembly;
        private readonly string[] _readmeRelativePath;

        public ReadmeTraceabilityChecker(DocSection section, Assembly assembly, string[] readmeRelativePath)
        {
            _section = section;
            _assembly = assembly;
            _readmeRelativePath = readmeRelativePath;
        }

        public int TaggedSymbolCount()
            => TaggedMethods().Count + TaggedTypes().Count;

        public IReadOnlyList<string> MethodFailures()
        {
            var blocks = DocumentedRegions();
            var failures = new List<string>();

            foreach (var method in TaggedMethods())
            {
                var named = blocks.Where(b => ContainsInvocation(b, method.Name)).ToList();
                if (named.Count == 0)
                {
                    failures.Add($"{Describe(method)} is tagged but no README code block calls or declares " +
                                 $"'{method.Name}(' - rename or removal drift.");
                    continue;
                }

                var parameters = method.GetParameters().Select(p => p.Name).ToArray();
                if (!named.Any(b => parameters.All(p => ContainsWholeWord(b, p))))
                {
                    var missing = parameters.Where(p => !named.Any(b => ContainsWholeWord(b, p))).ToArray();
                    failures.Add($"{Describe(method)}: no README block covers every parameter " +
                                 $"(missing: {string.Join(", ", missing)}) - signature drift.");
                    continue;
                }

                if (parameters.Length > 1 && !named.Any(b => MentionsInOrder(b, parameters)))
                {
                    failures.Add($"{Describe(method)}: every parameter is named somewhere but no README " +
                                 $"block lists them in declaration order ({string.Join(", ", parameters)}) - " +
                                 "argument ORDER drift, which a caller cannot see and the compiler will " +
                                 "not catch when the types match.");
                }
            }

            return failures;
        }

        public IReadOnlyList<string> EnumFailures()
        {
            var blocks = DocumentedRegions();
            var failures = new List<string>();

            foreach (var type in TaggedTypes().Where(t => t.IsEnum))
            {
                var members = Enum.GetNames(type);
                if (!blocks.Any(b => ContainsWholeWord(b, type.Name) && members.All(m => ContainsWholeWord(b, m))))
                {
                    var missing = members.Where(m => !blocks.Any(b => ContainsWholeWord(b, m))).ToArray();
                    failures.Add($"{type.Name}: no README block lists the enum and every member " +
                                 $"(missing: {string.Join(", ", missing)}).");
                }
            }

            return failures;
        }

        public IReadOnlyList<string> TypeSurfaceFailures()
        {
            var blocks = DocumentedRegions();
            var wholeReadme = File.ReadAllText(ReadmePath());
            var failures = new List<string>();

            foreach (var type in TaggedTypes().Where(t => !t.IsEnum))
            {
                var named = blocks.Where(b => ContainsWholeWord(b, type.Name)).ToList();
                if (named.Count == 0)
                {
                    failures.Add($"{type.Name} is tagged but is not named in any README code block.");
                    continue;
                }

                var surface = PublicSurfaceNames(type);
                if (surface.Length == 0) continue;

                var undocumented = surface
                    .Where(n => !ContainsWholeWord(wholeReadme, n))
                    .ToArray();

                if (undocumented.Length > 0)
                {
                    failures.Add($"{type.Name} is named in the README but these public members are " +
                                 $"documented nowhere in it: {string.Join(", ", undocumented)}.");
                    continue;
                }

                failures.AddRange(ParameterOrderFailures(type, blocks));
            }

            return failures;
        }

        private static string[] PublicSurfaceNames(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance |
                                       BindingFlags.Static | BindingFlags.DeclaredOnly;

            var properties = type.GetProperties(flags).Select(p => p.Name);
            var fields = type.GetFields(flags).Where(f => !f.IsSpecialName).Select(f => f.Name);
            var methods = type.GetMethods(flags)
                .Where(m => !m.IsSpecialName)
                .Where(m => !IsInheritedFromObject(m))
                .Select(m => m.Name);

            return properties.Concat(fields).Concat(methods)
                .Where(n => !IsCompilerGenerated(n))
                .Distinct()
                .ToArray();
        }

        private static bool IsCompilerGenerated(string name) =>
            name == "EqualityContract" || name == "Deconstruct" || name.StartsWith("<");

        private static bool IsInheritedFromObject(MethodInfo method) =>
            method.Name == "ToString" || method.Name == "Equals" ||
            method.Name == "GetHashCode" || method.Name == "GetType";

        private List<MethodInfo> TaggedMethods()
        {
            var result = new List<MethodInfo>();
            foreach (var type in LoadableTypes())
            {
                MethodInfo[] methods;
                try
                {
                    methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static |
                                              BindingFlags.Instance | BindingFlags.DeclaredOnly);
                }
                catch { continue; }

                foreach (var method in methods)
                    if (IsTagged(method)) result.Add(method);
            }
            return result;
        }

        private List<Type> TaggedTypes()
            => LoadableTypes().Where(IsTagged).ToList();

        private bool IsTagged(MemberInfo member)
        {
            try
            {
                return member.GetCustomAttributes<NethereumDocExampleAttribute>(false)
                    .Any(a => a.Section == _section);
            }
            catch { return false; }
        }

        private IEnumerable<Type> LoadableTypes()
        {
            try { return _assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
        }

        public string ReadmePath()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(new[] { dir.FullName }.Concat(_readmeRelativePath).ToArray());
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            throw new FileNotFoundException(
                "Could not locate " + string.Join("/", _readmeRelativePath) +
                " by walking up from " + AppContext.BaseDirectory);
        }

        private List<string> DocumentedRegions()
        {
            var regions = FencedBlocks();
            var table = new StringBuilder();

            foreach (var line in ReadmeLines())
            {
                if (line.TrimStart().StartsWith("|"))
                {
                    table.Append(line).Append('\n');
                }
                else if (table.Length > 0)
                {
                    regions.Add(table.ToString());
                    table.Clear();
                }
            }

            if (table.Length > 0) regions.Add(table.ToString());

            return regions;
        }

        private string[] ReadmeLines() =>
            File.ReadAllText(ReadmePath()).Replace("\r\n", "\n").Split('\n');

        private List<string> FencedBlocks()
        {
            var readme = File.ReadAllText(ReadmePath());
            var blocks = new List<string>();
            var current = new StringBuilder();
            var inBlock = false;

            foreach (var line in readme.Replace("\r\n", "\n").Split('\n'))
            {
                if (line.TrimStart().StartsWith("```"))
                {
                    if (inBlock) { blocks.Add(current.ToString()); current.Clear(); inBlock = false; }
                    else { inBlock = true; }
                    continue;
                }
                if (inBlock) current.Append(line).Append('\n');
            }

            return blocks;
        }

        private static IEnumerable<string> ParameterOrderFailures(Type type, List<string> blocks)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance |
                                       BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var method in type.GetMethods(flags))
            {
                if (method.IsSpecialName || IsInheritedFromObject(method)) continue;

                var parameters = method.GetParameters().Select(p => p.Name).ToArray();
                if (parameters.Length < 2) continue;

                var named = blocks.Where(b => ContainsInvocation(b, method.Name)).ToList();
                if (named.Count == 0) continue;

                if (!named.Any(b => MentionsInOrder(b, parameters)))
                {
                    yield return $"{type.Name}.{method.Name}: no README block lists its parameters in " +
                                 $"declaration order ({string.Join(", ", parameters)}) - argument ORDER " +
                                 "drift, which a caller cannot see and the compiler will not catch " +
                                 "when the types match.";
                }
            }
        }

        private static bool MentionsInOrder(string text, string[] words)
        {
            var searchFrom = 0;
            foreach (var word in words)
            {
                var at = IndexOfWholeWord(text, word, searchFrom);
                if (at < 0) return false;
                searchFrom = at + word.Length;
            }

            return true;
        }

        private static int IndexOfWholeWord(string text, string word, int startAt)
        {
            var match = Regex.Match(text.Substring(startAt),
                "(?<![A-Za-z0-9_])" + Regex.Escape(word) + "(?![A-Za-z0-9_])");
            return match.Success ? startAt + match.Index : -1;
        }

        private static bool ContainsWholeWord(string text, string word)
            => Regex.IsMatch(text, "(?<![A-Za-z0-9_])" + Regex.Escape(word) + "(?![A-Za-z0-9_])");

        private static bool ContainsInvocation(string text, string methodName)
            => Regex.IsMatch(text, "(?<![A-Za-z0-9_])" + Regex.Escape(methodName) + @"\s*(<[^>()]*>)?\s*\(");

        private static string Describe(MethodInfo method)
            => method.DeclaringType?.Name + "." + method.Name;
    }
}
