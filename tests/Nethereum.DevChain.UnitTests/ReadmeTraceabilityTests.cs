using System.Reflection;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.DevChain.UnitTests
{
    public class ReadmeTraceabilityTests
    {
        private static string ReadReadme()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "src", "Nethereum.DevChain", "README.md");
                if (File.Exists(candidate)) return File.ReadAllText(candidate);
                dir = dir.Parent;
            }
            Assert.Fail("Could not locate src/Nethereum.DevChain/README.md");
            return null!;
        }

        [Fact]
        public void TaggedProperties_ArePresentInTheReadmeConfigurationBlock()
        {
            var tagged = typeof(DevChainConfig).Assembly.GetTypes()
                .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(p => p.GetCustomAttributes<NethereumDocExampleAttribute>(false)
                    .Any(a => a.Section == DocSection.InProcessNode && a.UseCase == "devchain"))
                .ToList();
            Assert.NotEmpty(tagged);

            var readme = ReadReadme();
            foreach (var property in tagged)
                Assert.Contains($"public {(property.PropertyType == typeof(bool) ? "bool" : property.PropertyType.Name)} {property.Name} {{ get; set; }}", readme);
        }
    }
}
