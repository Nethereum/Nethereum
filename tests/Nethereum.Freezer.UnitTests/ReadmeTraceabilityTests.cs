using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.Freezer.UnitTests
{
    public class ReadmeTraceabilityTests
    {
        private const DocSection Section = DocSection.ChainInfrastructure;

        private static string FindReadmePath()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "src", "Nethereum.Freezer", "README.md");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        private static string ReadReadme()
        {
            var path = FindReadmePath();
            if (path == null)
            {
                Assert.Fail("Could not locate src/Nethereum.Freezer/README.md by walking up from " +
                            AppContext.BaseDirectory);
                return null;
            }
            return File.ReadAllText(path);
        }

        private static List<string> FencedBlocks(string readme)
        {
            var blocks = new List<string>();
            var sb = new StringBuilder();
            var inBlock = false;
            foreach (var line in readme.Replace("\r\n", "\n").Split('\n'))
            {
                if (line.TrimStart().StartsWith("```"))
                {
                    if (inBlock) { blocks.Add(sb.ToString()); sb.Clear(); inBlock = false; }
                    else { inBlock = true; }
                    continue;
                }
                if (inBlock) sb.Append(line).Append('\n');
            }
            return blocks;
        }

        private static bool ContainsWholeWord(string text, string word)
            => Regex.IsMatch(text, $@"(?<![A-Za-z0-9_]){Regex.Escape(word)}(?![A-Za-z0-9_])");

        private static IEnumerable<Type> LoadableTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
        }

        private static List<MethodInfo> TaggedMethods()
        {
            var result = new List<MethodInfo>();
            foreach (var type in LoadableTypes(typeof(Freezer).Assembly))
            {
                MethodInfo[] methods;
                try
                {
                    methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static |
                                              BindingFlags.Instance | BindingFlags.DeclaredOnly);
                }
                catch { continue; }
                foreach (var m in methods)
                {
                    if (m.GetCustomAttributes<NethereumDocExampleAttribute>(false).Any(a => a.Section == Section))
                        result.Add(m);
                }
            }
            return result;
        }

        [Fact]
        public void AtLeastOneSymbolTagged()
        {
            var count = TaggedMethods().Count;
            if (count == 0)
                Assert.Fail("traceability tags missing — no [NethereumDocExample(DocSection.ChainInfrastructure, ...)] " +
                            "methods found in the Nethereum.Freezer assembly");
        }

        [Fact]
        public void TaggedMethods_SignaturesPresentInReadme()
        {
            var readme = ReadReadme();
            var blocks = FencedBlocks(readme);
            var methods = TaggedMethods();
            if (methods.Count == 0)
                Assert.Fail("traceability tags missing — no tagged ChainInfrastructure methods found");

            foreach (var m in methods)
            {
                var name = m.Name;
                var blocksWithName = blocks.Where(b => b.Contains(name + "(")).ToList();

                if (blocksWithName.Count == 0)
                    Assert.Fail($"{name} tagged but no signature block in README — rename/removal drift");

                var paramNames = m.GetParameters().Select(p => p.Name).ToArray();

                bool fullyCovered = blocksWithName.Any(b => paramNames.All(p => ContainsWholeWord(b, p)));
                Assert.True(fullyCovered,
                    $"no README block fully covers the parameters of {name} — signature drift or missing signature block");
            }
        }
    }
}
