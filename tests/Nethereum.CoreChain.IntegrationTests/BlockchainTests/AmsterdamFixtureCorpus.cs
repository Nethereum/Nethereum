using System.IO;
using Nethereum.Testing;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    internal static class AmsterdamFixtureCorpus
    {
        internal static string Root =>
            Path.Combine(TestEnvironment.RequireExternalPath("EestAmsterdam"), "blockchain_tests", "amsterdam");

        internal static string Category(string name) => Path.Combine(Root, name);
    }
}
