using Nethereum.Testing;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    internal static class FrontierValidationFixtureCorpus
    {
        internal static string For(string fork) =>
            System.IO.Path.Combine(
                TestEnvironment.RequireExternalPath("EestStable"),
                "blockchain_tests", "for_" + fork.ToLowerInvariant(), "frontier", "validation");

        internal static string TransactionFile(string fork, string fileName) =>
            System.IO.Path.Combine(For(fork), "transaction", fileName);
    }
}
