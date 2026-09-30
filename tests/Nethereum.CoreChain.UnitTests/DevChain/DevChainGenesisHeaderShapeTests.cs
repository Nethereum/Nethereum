using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Genesis;
using Nethereum.DevChain;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.Codecs;
using Nethereum.Util;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class DevChainGenesisHeaderShapeTests
    {
        private const string PragueGenesisHash =
            "0x38fc49374345945a619f3b4e4118c3ab49b7689dd78486d1ff57222440da65ab";

        private const string AmsterdamGenesisHash =
            "0xe7c7af5bebda91d39539c24fedfc30c91f1d95e8bf573bb567394eea0ba86351";

        private const string EmptyRequestsHash =
            "0xe3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        [Theory]
        [InlineData("frontier")]
        [InlineData("berlin")]
        [InlineData("london")]
        [InlineData("shanghai")]
        [InlineData("cancun")]
        [InlineData("prague")]
        [InlineData("amsterdam")]
        public async Task Given_ADevChainAtAFork_When_ItStarts_Then_GenesisAndBlockOneAreTheSameShape(string fork)
        {
            using var node = new DevChainNode(new DevChainConfig { Hardfork = fork });
            await node.StartAsync();
            await node.MineBlockAsync();

            var genesis = await node.GetBlockByNumberAsync(0);
            var blockOne = await node.GetBlockByNumberAsync(1);

            Assert.Same(
                BlockHeaderCodecSelector.ForHeader(blockOne),
                BlockHeaderCodecSelector.ForHeader(genesis));
        }

        [Theory]
        [InlineData("frontier")]
        [InlineData("berlin")]
        [InlineData("london")]
        [InlineData("shanghai")]
        [InlineData("cancun")]
        [InlineData("prague")]
        [InlineData("amsterdam")]
        public async Task Given_ADevChainAtAnyFork_When_ItMinesABlock_Then_ItsStoredHashIsTheOneItsShapeEncodesTo(string fork)
        {
            using var node = new DevChainNode(new DevChainConfig { Hardfork = fork });
            await node.StartAsync();
            await node.MineBlockAsync();

            var blockOne = await node.GetBlockByNumberAsync(1);

            Assert.Equal(
                (await node.GetBlockHashByNumberAsync(1)).ToHex(true),
                new Sha3Keccack().CalculateHash(
                    BlockHeaderCodecSelector.ForHeader(blockOne).Encode(blockOne)).ToHex(true));
        }

        [Theory]
        [InlineData("frontier")]
        [InlineData("berlin")]
        [InlineData("london")]
        [InlineData("shanghai")]
        [InlineData("cancun")]
        [InlineData("prague")]
        [InlineData("amsterdam")]
        public async Task Given_ADevChainAtAnyFork_When_ItStarts_Then_GenesisShapeIsTheOneThatForksCodecEmits(string fork)
        {
            using var node = new DevChainNode(new DevChainConfig { Hardfork = fork });
            await node.StartAsync();

            var genesis = await node.GetBlockByNumberAsync(0);

            Assert.Same(
                Nethereum.Model.Codecs.BlockHeaderCodecs.ForFork(Nethereum.EVM.HardforkNames.Parse(fork)),
                BlockHeaderCodecSelector.ForHeader(genesis));
        }

        [Theory]
        [InlineData("prague", PragueGenesisHash)]
        [InlineData("amsterdam", AmsterdamGenesisHash)]
        public async Task Given_ADevChainAtAFork_When_ItStarts_Then_ItsGenesisHashIsThePinnedOne(string fork, string expected)
        {
            using var node = new DevChainNode(new DevChainConfig { Hardfork = fork });
            await node.StartAsync();

            Assert.Equal(expected, (await node.GetBlockHashByNumberAsync(0)).ToHex(true));
        }

        [Fact]
        public async Task Given_TheExecutionApisReferenceGenesisJsonWithAnUnpaddedNonce_When_TheConstructedGenesisIsBuilt_Then_ItsHashMatchesTheGethGoldenHash()
        {
            const string gethGoldenHash =
                "0x79e0f5ffb6a0c9f54d507dbbadc935603ac1db86d32f7857472180a75ec11f90";

            var genesisPath = Path.Combine(FindRepoRoot(), "external", "execution-apis", "tests", "genesis.json");
            Assert.True(File.Exists(genesisPath),
                $"ethereum/execution-apis fixture missing at {genesisPath}. See external/README.md.");

            var document = StandardGenesisLoader.LoadFromFile(genesisPath);

            var config = new DevChainConfig();
            StandardGenesisLoader.ApplyToChainConfig(config, document);

            using var node = DevChainNode.CreateInMemory(config);
            await StandardGenesisLoader.PopulateAllocAsync(node.State, document.Alloc);
            await node.StartAsync();

            Assert.Equal(8, config.GenesisNonce.Length);
            Assert.Equal(gethGoldenHash, (await node.GetBlockHashByNumberAsync(0)).ToHex(true));
        }

        [Fact]
        public async Task Given_ADevChainAtAmsterdam_When_ItStarts_Then_GenesisCarriesTheAmsterdamHeaderFields()
        {
            using var node = new DevChainNode(new DevChainConfig { Hardfork = "amsterdam" });
            await node.StartAsync();

            var genesis = await node.GetBlockByNumberAsync(0);

            Assert.NotNull(genesis.WithdrawalsRoot);
            Assert.Equal(new byte[32].ToHex(true), genesis.ParentBeaconBlockRoot.ToHex(true));
            Assert.Equal(EmptyRequestsHash, genesis.RequestsHash.ToHex(true));
            Assert.NotNull(genesis.BlockAccessListHash);
            Assert.Equal((ulong)0, genesis.SlotNumber);
        }

        [Fact]
        public async Task Given_ADevChainBeforeShanghai_When_ItStarts_Then_GenesisCarriesNoPostLondonFields()
        {
            using var node = new DevChainNode(new DevChainConfig { Hardfork = "london" });
            await node.StartAsync();

            var genesis = await node.GetBlockByNumberAsync(0);

            Assert.Null(genesis.WithdrawalsRoot);
            Assert.Null(genesis.ParentBeaconBlockRoot);
            Assert.Null(genesis.RequestsHash);
            Assert.Null(genesis.BlockAccessListHash);
            Assert.Null(genesis.SlotNumber);
        }

        [Fact]
        public async Task Given_AStoredLondonShapedGenesis_When_ADevChainRestartsAtPrague_Then_ItRefusesToStart()
        {
            using var store = new PersistedDevChainStore();
            using (var london = store.NodeAt("london")) await london.StartAsync();

            using var prague = store.NodeAt("prague");
            var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => prague.StartAsync());

            Assert.Contains("London", refusal.Message);
            Assert.Contains("Prague", refusal.Message);
        }

        [Fact]
        public async Task Given_AStoredLondonShapedGenesis_When_ADevChainRestartsAtLondon_Then_ItStarts()
        {
            using var store = new PersistedDevChainStore();
            using (var first = store.NodeAt("london")) await first.StartAsync();

            using var second = store.NodeAt("london");
            await second.StartAsync();
        }

        [Fact]
        public void Given_ADevChainGenesisWrittenByTheOlderBuilder_When_ItIsCheckedAgainstPrague_Then_ItIsRefused()
        {
            var refusal = Assert.Throws<InvalidOperationException>(
                () => GenesisHeaderFields.EnsureShapeMatchesPinnedFork(
                    LondonShapedGenesisWithMiningEraScalars(), HardforkName.Prague));

            Assert.Contains("London", refusal.Message);
            Assert.Contains("Prague", refusal.Message);
        }

        [Fact]
        public void Given_ADevChainGenesisWrittenByTheOlderBuilder_When_ItIsCheckedAgainstLondon_Then_ItIsAccepted()
            => GenesisHeaderFields.EnsureShapeMatchesPinnedFork(
                LondonShapedGenesisWithMiningEraScalars(), HardforkName.London);

        private static BlockHeader LondonShapedGenesisWithMiningEraScalars()
            => new BlockHeader
            {
                BlockNumber = 0,
                ParentHash = new byte[32],
                UnclesHash = new byte[32],
                Coinbase = "0x0000000000000000000000000000000000000000",
                StateRoot = new byte[32],
                TransactionsHash = DefaultValues.EMPTY_TRIE_HASH,
                ReceiptHash = DefaultValues.EMPTY_TRIE_HASH,
                LogsBloom = new byte[256],
                Difficulty = 1,
                GasLimit = 30_000_000,
                GasUsed = 0,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                BaseFee = 1_000_000_000
            };

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                    return dir.FullName;
                dir = dir.Parent;
            }

            return Directory.GetCurrentDirectory();
        }

        private sealed class PersistedDevChainStore : IDisposable
        {
            private readonly string _path =
                Path.Combine(Path.GetTempPath(), $"devchain_shape_{Guid.NewGuid():N}.db");

            public DevChainNode NodeAt(string fork) => new DevChainNode(
                new DevChainConfig { Hardfork = fork, AutoMine = false }, _path, persistDb: true);

            public void Dispose()
            {
                try { File.Delete(_path); } catch (IOException) { }
            }
        }
    }
}
