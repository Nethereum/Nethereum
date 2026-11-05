using System.IO;
using Nethereum.Testing;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    internal static class Eip7918BlobReservePriceFixtureCorpus
    {
        internal static string Root =>
            Path.Combine(TestEnvironment.RequireExternalPath("EestFusakaDevnet5"), "blockchain_tests");

        internal static string Category(string testFolder) =>
            Path.Combine(Root, "osaka", "eip7918_blob_reserve_price", testFolder);
    }
}
