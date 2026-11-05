using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Nethereum.ChainNode.Hosting.Rpc;
using Nethereum.Documentation;
using Nethereum.EngineApi.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class ChainHostingReadmeTraceabilityTests
    {
        public static IEnumerable<object[]> Packages()
        {
            yield return new object[] { "Nethereum.ChainNode.Hosting", typeof(RpcHttpEndpoint).Assembly.FullName! };
            yield return new object[] { "Nethereum.EngineApi.Hosting", typeof(EngineApiHostingExtensions).Assembly.FullName! };
        }

        private static string ReadReadme(string package)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "src", package, "README.md");
                if (File.Exists(candidate)) return File.ReadAllText(candidate);
                dir = dir.Parent;
            }
            Assert.Fail($"Could not locate src/{package}/README.md by walking up from {AppContext.BaseDirectory}");
            return null!;
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

        private static List<MethodInfo> TaggedMethods(Assembly assembly)
        {
            var result = new List<MethodInfo>();
            foreach (var type in assembly.GetTypes())
            {
                foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (m.GetCustomAttributes<NethereumDocExampleAttribute>(false).Any(a => a.Section == DocSection.ChainInfrastructure))
                        result.Add(m);
                }
            }
            return result;
        }

        [Theory]
        [MemberData(nameof(Packages))]
        public void TaggedMethods_SignaturesPresentInReadme(string package, string assemblyName)
        {
            var assembly = Assembly.Load(new AssemblyName(assemblyName));
            var methods = TaggedMethods(assembly);
            Assert.NotEmpty(methods);
            var blocks = FencedBlocks(ReadReadme(package));

            foreach (var m in methods)
            {
                var withName = blocks.Where(b => b.Contains(m.Name + "(")).ToList();
                Assert.True(withName.Count > 0, $"{m.Name} is tagged but no signature block in the {package} README names it");
                var paramNames = m.GetParameters().Select(p => p.Name!).ToArray();
                Assert.True(withName.Any(b => paramNames.All(p => ContainsWholeWord(b, p))),
                    $"no {package} README block covers every parameter of {m.Name}");
            }
        }
    }
}
