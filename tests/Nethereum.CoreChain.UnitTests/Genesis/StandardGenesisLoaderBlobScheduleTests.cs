using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Genesis;
using Nethereum.CoreChain.Storage.InMemory;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Genesis
{
    public class StandardGenesisLoaderBlobScheduleTests
    {
        private const string Alloc = "\"alloc\":{\"0x1000000000000000000000000000000000000001\":{\"balance\":\"0x3635c9adc5dea00000\"}}";

        private static string GenesisJson(string extraConfig) =>
            "{\"config\":{\"chainId\":7331,\"shanghaiTime\":0,\"cancunTime\":0,\"pragueTime\":0," + extraConfig + "}," +
            "\"gasLimit\":\"0x1c9c380\",\"difficulty\":\"0x0\",\"timestamp\":\"0x0\"," + Alloc + "}";

        [Fact]
        public void Given_AGenesisWithABlobScheduleAndBpoTimes_When_TheForkScheduleIsBuilt_Then_EveryEntryIsCarriedWithItsParametersAndBpoEntriesWithTheirTimestamps()
        {
            var json = GenesisJson(
                "\"bpo1Time\":1000,\"bpo2Time\":2000," +
                "\"blobSchedule\":{" +
                "\"cancun\":{\"target\":3,\"max\":6,\"baseFeeUpdateFraction\":3338477}," +
                "\"prague\":{\"target\":6,\"max\":9,\"baseFeeUpdateFraction\":5007716}," +
                "\"bpo1\":{\"target\":10,\"max\":15,\"baseFeeUpdateFraction\":8346193}," +
                "\"bpo2\":{\"target\":14,\"max\":21,\"baseFeeUpdateFraction\":11684671}}");

            var document = StandardGenesisLoader.Parse(json);
            var schedule = StandardGenesisLoader.BuildForkSchedule(document);

            Assert.Equal(new[] { "cancun", "prague", "bpo1", "bpo2" }, schedule.BlobSchedule.Select(e => e.Name).ToArray());
            var bpo1 = schedule.BlobSchedule.Single(e => e.Name == "bpo1");
            Assert.Equal(1000UL, bpo1.Timestamp);
            Assert.Equal(10, bpo1.Target);
            Assert.Equal(15, bpo1.Max);
            Assert.Equal(8346193, bpo1.BaseFeeUpdateFraction);
            Assert.Equal(2000UL, schedule.BlobSchedule.Single(e => e.Name == "bpo2").Timestamp);
            Assert.Null(schedule.BlobSchedule.Single(e => e.Name == "prague").Timestamp);
        }

        [Fact]
        public void Given_ABpoBlobScheduleEntryWithNoBpoTime_When_TheForkScheduleIsBuilt_Then_ItIsLeftOut()
        {
            var json = GenesisJson(
                "\"blobSchedule\":{" +
                "\"prague\":{\"target\":6,\"max\":9,\"baseFeeUpdateFraction\":5007716}," +
                "\"bpo1\":{\"target\":10,\"max\":15,\"baseFeeUpdateFraction\":8346193}}");

            var schedule = StandardGenesisLoader.BuildForkSchedule(StandardGenesisLoader.Parse(json));

            Assert.Equal(new[] { "prague" }, schedule.BlobSchedule.Select(e => e.Name).ToArray());
        }

        [Fact]
        public void Given_AGenesisWithNoBlobSchedule_When_TheForkScheduleIsBuilt_Then_TheBlobScheduleIsEmpty()
        {
            var schedule = StandardGenesisLoader.BuildForkSchedule(StandardGenesisLoader.Parse(GenesisJson("\"terminalTotalDifficulty\":0")));

            Assert.Empty(schedule.BlobSchedule);
        }

        [Fact]
        public void Given_AGenesisThatDeclaresASystemContract_When_Checked_Then_ItIsDetectedCaseInsensitivelyAndAnOrdinaryAllocIsNot()
        {
            var plain = StandardGenesisLoader.Parse(GenesisJson("\"terminalTotalDifficulty\":0"));
            Assert.False(StandardGenesisLoader.DeclaresAnySystemContract(plain));

            var declaring = StandardGenesisLoader.Parse(
                "{\"config\":{\"chainId\":7331},\"alloc\":{\"0x0000F90827F1C53A10CB7A02335B175320002935\":{\"balance\":\"0x0\",\"code\":\"0x00\"}}}");
            Assert.True(StandardGenesisLoader.DeclaresAnySystemContract(declaring));
        }

        [Fact]
        public async Task Given_AGenesisDocument_When_TheInitializerRunsTwice_Then_TheSameGenesisHashIsReturnedAndTheStoredBlockZeroCarriesTheAllocationRoot()
        {
            var document = StandardGenesisLoader.Parse(GenesisJson("\"terminalTotalDifficulty\":0"));
            var chain = new ChainConfig { ChainId = 7331 };
            StandardGenesisLoader.ApplyToChainConfig(chain, document);
            using var stores = InMemoryChainStoreBundle.Open();

            var first = await GenesisInitializer.EnsureAsync(stores, document, chain);
            var second = await GenesisInitializer.EnsureAsync(stores, document, chain);

            Assert.Equal(first, second);
            Assert.True(stores.Metadata.IsGenesisLoaded());
            var stored = await stores.Blocks.GetByNumberAsync(0);
            Assert.NotNull(stored);
            Assert.Equal(first, await stores.Blocks.GetHashByNumberAsync(System.Numerics.BigInteger.Zero));
            Assert.NotEqual(Nethereum.Model.DefaultValues.EMPTY_TRIE_HASH, stored.StateRoot);
            var historyAddress = Nethereum.CoreChain.Forks.SystemContractPredeploys.For(Nethereum.EVM.HardforkName.Prague)
                .Single(p => p.Address.EndsWith("2935", StringComparison.OrdinalIgnoreCase)).Address;
            var history = await stores.State.GetAccountAsync(historyAddress);
            Assert.NotNull(history);
            Assert.NotEqual(Nethereum.Model.DefaultValues.EMPTY_DATA_HASH, history.CodeHash);
        }

        [Fact]
        public async Task Given_AStoreHoldingOneGenesis_When_AnotherGenesisIsInitialised_Then_TheStartIsRefused()
        {
            var document = StandardGenesisLoader.Parse(GenesisJson("\"terminalTotalDifficulty\":0"));
            var chain = new ChainConfig { ChainId = 7331 };
            StandardGenesisLoader.ApplyToChainConfig(chain, document);
            using var stores = InMemoryChainStoreBundle.Open();
            await GenesisInitializer.EnsureAsync(stores, document, chain);

            var other = new ChainConfig { ChainId = 7331 };
            StandardGenesisLoader.ApplyToChainConfig(other, document);
            other.GenesisExtraData = new byte[] { 1, 2, 3 };

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => GenesisInitializer.EnsureAsync(stores, document, other));
            Assert.Contains("different genesis", error.Message);
        }
    }
}
