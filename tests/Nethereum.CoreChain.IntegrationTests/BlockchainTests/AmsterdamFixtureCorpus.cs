using System.IO;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    internal static class AmsterdamFixtureCorpus
    {
        internal static string Root { get; } = FindRoot();

        internal static string Category(string name) => Path.Combine(Root ?? "", name);

        private static string FindRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                {
                    var path = Path.Combine(dir.FullName, "external", "execution-spec-tests", "fixtures", "blockchain_tests", "amsterdam");
                    if (Directory.Exists(path)) return path;
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
