using System.IO;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    internal static class Eip7918BlobReservePriceFixtureCorpus
    {
        internal static string Root { get; } = FindRoot();

        internal static string Category(string testFolder) =>
            Root == null ? null : Path.Combine(Root, "osaka", "eip7918_blob_reserve_price", testFolder);

        private static string FindRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                {
                    return Path.Combine(dir.FullName, "external", "execution-spec-tests", "fixtures", "blockchain_tests");
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
