using System.IO;
using System.Reflection;
using Xunit;

namespace Nethereum.Signer.Bls.Tests
{
    public class BlsVectorManifestTests
    {
        [Fact]
        public void VectorDirectories_Are_Present()
        {
            var root = Path.GetFullPath(Path.Combine(GetProjectDirectory(), "..", "LightClientVectors", "bls"));
            Assert.True(Directory.Exists(root), "BLS vector folder should exist for future fixtures.");
        }

        [Fact]
        public void VectorReadme_References_OfficialSources()
        {
            var readme = Path.GetFullPath(Path.Combine(GetProjectDirectory(), "..", "LightClientVectors", "README.md"));
            Assert.True(File.Exists(readme), "Vector README documents canonical sources.");
            var contents = File.ReadAllText(readme);
            Assert.Contains("consensus-spec-tests", contents);
            Assert.Contains("bls12-381", contents);
        }

        private static string GetProjectDirectory()
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var binDirectory = Path.GetDirectoryName(assemblyPath)!;
            return Path.GetFullPath(Path.Combine(binDirectory, "..", "..", ".."));
        }
    }
}
