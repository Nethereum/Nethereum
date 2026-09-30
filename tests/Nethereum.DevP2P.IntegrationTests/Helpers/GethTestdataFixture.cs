using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Helpers
{
    public class GethTestdataFixture
    {
        public string TestdataPath { get; }

        public GethTestdataHistoricalStateBuilder.Result HistoricalState { get; }

        public GethTestdataFixture()
        {
            TestdataPath = GethToolLocator.FindEthTestTestdata();
            HistoricalState = GethTestdataHistoricalStateBuilder.Build(TestdataPath);
        }
    }

    [CollectionDefinition(Name)]
    public class GethTestdataCollection : ICollectionFixture<GethTestdataFixture>
    {
        public const string Name = "geth-testdata";
    }
}
