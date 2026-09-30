using System.IO;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    internal static class FrontierValidationFixtureCorpus
    {
        internal static string Path { get; } = FindPath();

        private static string FindPath()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(System.IO.Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(System.IO.Path.Combine(dir.FullName, "Nethereum.sln")))
                {
                    var path = System.IO.Path.Combine(
                        dir.FullName, "external", "execution-spec-tests", "fixtures",
                        "blockchain_tests", "frontier", "validation");
                    if (Directory.Exists(path)) return path;
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
