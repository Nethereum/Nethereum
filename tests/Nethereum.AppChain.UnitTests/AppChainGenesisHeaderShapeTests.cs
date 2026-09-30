using System;
using System.Threading.Tasks;
using Nethereum.AppChain.Genesis;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Model.SSZ;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AppChain.UnitTests
{
    public class AppChainGenesisHeaderShapeTests
    {
        private const string LondonGenesisHash =
            "0xe9fa5b1dd36023fb2b87a69207ea17eb0c00d8d1c4f0930ebff7862dde8ef7a8";

        private const string PragueGenesisHash =
            "0xe2cefb48509f20d77bcd1439930e7f6a204b8ec7e42de7493b5ffa1d5919ec7a";

        private const string AmsterdamGenesisHash =
            "0x7b274cb643d397d8181e48ebf8e03238201e938091d9f880c5044210db9c1e19";

        // The same Prague genesis under the EIP-7807 SSZ hashing scheme, whose
        // provider defines the hash itself and lets no header codec near it.
        // Provenance, weaker than its siblings and stated so: this value was
        // taken from our own SszBlockHeaderEncoder. No external oracle publishes
        // it, so it pins the value against drift and vouches for nothing else.
        private const string SszPragueGenesisHash =
            "0xd624b2a1e10cd6e4108969830b3bf8bb28236f5175ae231171615726e230e599";

        // keccak(rlp([])) — the empty EIP-7928 block access list. Checked against
        // the Amsterdam genesis header of the execution-spec-tests fixture
        // eip7843_slotnum/slotnum/slotnum_genesis.json, which states this value
        // for a genesis that executes nothing.
        private const string EmptyBlockAccessListHash =
            "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347";

        // EIP-7685: "For a block with no requests data, the requests_hash is
        // simply sha256("")." Same value the Amsterdam EEST genesis fixture
        // carries; the builder used to write 32 zero bytes.
        private const string EmptyRequestsHash =
            "0xe3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        private static async Task<GenesisBlockResult> BuildGenesisAt(string fork)
        {
            var config = AppChainConfig.Default;
            config.Hardfork = fork;
            return await new AppChainGenesisBuilder(config, new InMemoryStateStore()).BuildGenesisBlockAsync();
        }

        [Fact]
        public async Task Given_APinnedAmsterdamAppChain_When_GenesisIsBuilt_Then_ItsHashIsThePinnedAmsterdamOne()
        {
            var genesis = await BuildGenesisAt("amsterdam");

            Assert.Equal(AmsterdamGenesisHash, genesis.BlockHash.ToHex(true));
        }

        [Fact]
        public async Task Given_APinnedAmsterdamAppChain_When_GenesisIsBuilt_Then_ItsHeaderHashesThroughTheAmsterdamCodec()
        {
            var genesis = await BuildGenesisAt("amsterdam");

            var throughAmsterdam = new Sha3Keccack().CalculateHash(
                AmsterdamBlockHeaderCodec.Instance.Encode(genesis.Header));

            Assert.Equal(throughAmsterdam.ToHex(true), genesis.BlockHash.ToHex(true));
        }

        [Fact]
        public async Task Given_APinnedAmsterdamAppChain_When_GenesisIsBuilt_Then_ItCarriesTheAmsterdamHeaderFields()
        {
            var genesis = await BuildGenesisAt("amsterdam");

            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH.ToHex(true), genesis.Header.WithdrawalsRoot.ToHex(true));
            Assert.Equal((long)0, genesis.Header.BlobGasUsed);
            Assert.Equal((long)0, genesis.Header.ExcessBlobGas);
            Assert.Equal(new byte[32].ToHex(true), genesis.Header.ParentBeaconBlockRoot.ToHex(true));
            Assert.Equal(EmptyRequestsHash, genesis.Header.RequestsHash.ToHex(true));
            Assert.Equal(EmptyBlockAccessListHash, genesis.Header.BlockAccessListHash.ToHex(true));
            Assert.Equal((ulong)0, genesis.Header.SlotNumber);
        }

        [Fact]
        public async Task Given_APinnedPreAmsterdamAppChain_When_GenesisIsBuilt_Then_ItsGenesisHashIsUnchanged()
        {
            var genesis = await BuildGenesisAt("london");

            Assert.Equal(LondonGenesisHash, genesis.BlockHash.ToHex(true));
            Assert.Null(genesis.Header.WithdrawalsRoot);
            Assert.Null(genesis.Header.RequestsHash);
            Assert.Null(genesis.Header.BlockAccessListHash);
            Assert.Null(genesis.Header.SlotNumber);
        }

        [Theory]
        [InlineData("frontier")]
        [InlineData("berlin")]
        [InlineData("london")]
        [InlineData("shanghai")]
        [InlineData("cancun")]
        [InlineData("prague")]
        [InlineData("amsterdam")]
        public async Task Given_AnyPinnedFork_When_GenesisIsBuilt_Then_ItsShapeIsTheOneThatForksCodecEmits(string fork)
        {
            var genesis = await BuildGenesisAt(fork);

            var byShape = BlockHeaderCodecSelector.ForHeader(genesis.Header);
            var byFork = BlockHeaderCodecs.ForFork(Nethereum.EVM.HardforkNames.Parse(fork));

            Assert.Same(byFork, byShape);
            Assert.Equal(
                new Sha3Keccack().CalculateHash(byShape.Encode(genesis.Header)).ToHex(true),
                genesis.BlockHash.ToHex(true));
        }

        [Fact]
        public async Task Given_APinnedPragueAppChain_When_GenesisIsBuilt_Then_ItsHashIsThePinnedPragueOne()
        {
            var prague = await BuildGenesisAt("prague");

            Assert.Equal(PragueGenesisHash, prague.BlockHash.ToHex(true));
            Assert.NotEqual(LondonGenesisHash, prague.BlockHash.ToHex(true));
            Assert.Equal(EmptyRequestsHash, prague.Header.RequestsHash.ToHex(true));
            Assert.Equal(new byte[32].ToHex(true), prague.Header.ParentBeaconBlockRoot.ToHex(true));
            Assert.Null(prague.Header.BlockAccessListHash);
            Assert.Null(prague.Header.SlotNumber);
        }

        /// <summary>
        /// The SSZ route has a genesis identity too, and it moves for the same
        /// reason: <c>hash_tree_root</c> is taken over the header AFTER the
        /// fork's fields are stamped, so a null field that used to hash as 32
        /// zero bytes now hashes as the empty-trie root and sha256(""). Nothing
        /// pinned it before this.
        ///
        /// <para>Recorded gap, outside AMS-HDR-17: the EIP-7807 container
        /// carries no EIP-7928 block-access-list hash and no EIP-7843 slot
        /// number, so an SSZ Amsterdam genesis commits to neither. That is the
        /// SSZ encoder's field set, not the genesis builder's, and pinning a
        /// hash for it here would ratify the omission.</para>
        /// </summary>
        [Fact]
        public async Task Given_AnSszHashedAppChain_When_GenesisIsBuilt_Then_ItsHashIsThePinnedSszOne()
        {
            var config = AppChainConfig.Default;
            config.Hardfork = "prague";

            var genesis = await new AppChainGenesisBuilder(
                config, new InMemoryStateStore(), SszSha256BlockHashProvider.Instance).BuildGenesisBlockAsync();

            Assert.Equal(SszPragueGenesisHash, genesis.BlockHash.ToHex(true));
        }

        [Fact]
        public async Task Given_AStoredLondonShapedGenesis_When_APragueAppChainRestarts_Then_ItRefusesToRun()
        {
            var stores = new AppChainStores();
            await stores.SaveGenesisBuiltAtAsync("london");

            var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
                () => stores.OpenAppChainAt("prague").InitializeAsync());

            Assert.Contains("London", refusal.Message);
            Assert.Contains("Prague", refusal.Message);
        }

        [Fact]
        public async Task Given_AStoredLondonShapedGenesis_When_ALondonAppChainRestarts_Then_ItStarts()
        {
            var stores = new AppChainStores();
            await stores.SaveGenesisBuiltAtAsync("london");

            await stores.OpenAppChainAt("london").InitializeAsync();
        }

        private sealed class AppChainStores
        {
            private readonly InMemoryBlockStore _blocks = new InMemoryBlockStore();
            private readonly InMemoryStateStore _state = new InMemoryStateStore();

            public async Task SaveGenesisBuiltAtAsync(string fork)
            {
                var config = AppChainConfig.Default;
                config.Hardfork = fork;
                var genesis = await new AppChainGenesisBuilder(config, _state).BuildGenesisBlockAsync();
                await _blocks.SaveAsync(genesis.Header, genesis.BlockHash);
            }

            public AppChain OpenAppChainAt(string fork)
            {
                var config = AppChainConfig.Default;
                config.Hardfork = fork;
                return new AppChain(
                    config, _blocks, new InMemoryTransactionStore(_blocks),
                    new InMemoryReceiptStore(), new InMemoryLogStore(), _state);
            }
        }
    }
}
