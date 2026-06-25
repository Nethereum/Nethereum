using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Helpers
{
    /// <summary>
    /// Eagerly builds the historical state of Geth's eth-test testdata chain
    /// at fixture-construction time so the ~2 second block-replay cost is paid
    /// once per test collection, not on whichever test happens to run first.
    /// <para>
    /// Without this, <c>LargeTxRequest</c> (which sits behind Geth's 2 second
    /// read deadline) flakes whenever it runs as the first test in the process.
    /// </para>
    /// </summary>
    public class GethTestdataFixture
    {
        /// <summary>Resolved absolute path of <c>cmd/devp2p/internal/ethtest/testdata</c>.</summary>
        public string TestdataPath { get; }

        /// <summary>Replayed state at chain head + ENRs of every block — cached once per test collection.</summary>
        public GethTestdataHistoricalStateBuilder.Result HistoricalState { get; }

        public GethTestdataFixture()
        {
            TestdataPath = GethToolLocator.FindEthTestTestdata();
            HistoricalState = GethTestdataHistoricalStateBuilder.Build(TestdataPath);
        }
    }

    /// <summary>
    /// xUnit collection that shares <see cref="GethTestdataFixture"/> across tests so the
    /// historical state replay runs only once per test process.
    /// </summary>
    [CollectionDefinition(Name)]
    public class GethTestdataCollection : ICollectionFixture<GethTestdataFixture>
    {
        public const string Name = "geth-testdata";
    }
}
