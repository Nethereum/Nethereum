using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Genesis;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.AppChain.UnitTests
{
    public class AppChainGenesisTrieNodeTests
    {
        private static readonly Sha3Keccack Keccak = new Sha3Keccack();

        private static byte[] HashedAddressKey(string address) =>
            Keccak.CalculateHash(AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray());

        private static AppChainConfig PragueConfig()
        {
            var config = AppChainConfig.Default;
            config.Hardfork = "prague";
            return config;
        }

        private sealed class Genesis
        {
            public InMemoryStateStore State { get; } = new InMemoryStateStore();
            public InMemoryContentNodeStore Nodes { get; } = new InMemoryContentNodeStore();
            public AppChainGenesisBuilder Builder { get; }

            public Genesis() => Builder = new AppChainGenesisBuilder(PragueConfig(), State, null, Nodes);
        }

        [Fact]
        public async Task Given_AnAppChainGenesis_When_ItIsBuilt_Then_TheGenesisRootIsWalkableFromThePersistedNodes()
        {
            var g = new Genesis();

            var genesis = await g.Builder.BuildGenesisBlockAsync();

            var trie = PatriciaTrie.LoadFromStorage(genesis.Header.StateRoot, g.Nodes);
            var accounts = await g.State.GetAllAccountsAsync();
            Assert.NotEmpty(accounts);

            foreach (var kv in accounts)
            {
                var encoded = trie.Get(HashedAddressKey(kv.Key));
                Assert.True(encoded != null && encoded.Length > 0,
                    $"account {kv.Key} is in flat state but not reachable from the genesis root through the persisted nodes");

                var fromTrie = AccountEncoder.Current.Decode(encoded);
                Assert.Equal(kv.Value.Balance, fromTrie.Balance);
                Assert.Equal(kv.Value.Nonce, fromTrie.Nonce);
            }
        }

        [Fact]
        public async Task Given_AGenesisAccountWithNonEmptyStorage_When_ItIsBuilt_Then_ItsAccountRecordsThatStorageRoot()
        {
            var g = new Genesis();
            const string address = "0x1111111111111111111111111111111111111111";
            var slotValue = new byte[32];
            slotValue[31] = 7;

            await g.State.SaveAccountAsync(address, new Account { Balance = new BigInteger(1) });
            await g.State.SaveStorageAsync(address, BigInteger.One, slotValue);

            var genesis = await g.Builder.BuildGenesisBlockAsync();

            var stateTrie = PatriciaTrie.LoadFromStorage(genesis.Header.StateRoot, g.Nodes);
            var account = AccountEncoder.Current.Decode(stateTrie.Get(HashedAddressKey(address)));

            Assert.NotEqual(
                DefaultValues.EMPTY_TRIE_HASH.ToHex(),
                account.StateRoot.ToHex());

            var storageTrie = PatriciaTrie.LoadFromStorage(account.StateRoot, g.Nodes);
            var stored = storageTrie.Get(AccountStorage.EncodeKeyForStorage(new byte[] { 1 }, Sha3KeccackHashProvider.Instance));
            Assert.True(stored != null && stored.Length > 0,
                "the account records a storage root whose trie is not walkable from the persisted nodes");
        }

        [Fact]
        public async Task Given_AnAppChainGenesis_When_ItCompletes_Then_NoAccountRemainsMarkedDirty()
        {
            var g = new Genesis();

            await g.Builder.BuildGenesisBlockAsync();

            Assert.Empty(await g.State.GetDirtyAccountAddressesAsync());
        }

        [Fact]
        public async Task Given_AGenesisFollowedByOneBlock_When_TheRootIsComputedIncrementally_Then_ItWalksOnlyThatBlocksAccountsAndStillMatchesAFullRecompute()
        {
            var g = new Genesis();
            await g.Builder.BuildGenesisBlockAsync();

            const string touched = "0x2222222222222222222222222222222222222222";
            await g.State.SaveAccountAsync(touched, new Account { Balance = new BigInteger(500) });

            var dirty = await g.State.GetDirtyAccountAddressesAsync();
            Assert.Single(dirty);

            var incremental = await new IncrementalStateRootCalculator(g.State, g.Nodes).ComputeStateRootAsync();

            var recomputeNodes = new InMemoryContentNodeStore();
            var fullRecompute = await new StateRootCalculator().ComputeStateRootAsync(g.State, recomputeNodes);

            Assert.Equal(fullRecompute.ToHex(), incremental.ToHex());
        }

        [Fact]
        public async Task Given_AFollowerBootstrappedByApplyGenesisState_When_ItImportsItsFirstBlock_Then_ItsRootMatches()
        {
            var built = new Genesis();
            var expected = await built.Builder.BuildGenesisBlockAsync();

            var bootstrapped = new Genesis();
            await bootstrapped.Builder.ApplyGenesisStateAsync();
            var bootstrappedRoot = await bootstrapped.Builder.PersistStateTrieAsync();

            Assert.Equal(expected.Header.StateRoot.ToHex(), bootstrappedRoot.ToHex());

            var trie = PatriciaTrie.LoadFromStorage(bootstrappedRoot, bootstrapped.Nodes);
            foreach (var kv in await bootstrapped.State.GetAllAccountsAsync())
            {
                var encoded = trie.Get(HashedAddressKey(kv.Key));
                Assert.True(encoded != null && encoded.Length > 0,
                    $"a follower bootstrapped through ApplyGenesisStateAsync cannot walk to {kv.Key} from its own genesis root");
            }
        }
    }
}
