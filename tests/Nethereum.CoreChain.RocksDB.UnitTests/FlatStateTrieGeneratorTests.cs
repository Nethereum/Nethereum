using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FlatStateTrieGeneratorTests : IDisposable
    {
        private readonly List<string> _dirs = new();
        private readonly List<RocksDbChainStoreBundle> _bundles = new();

        public void Dispose()
        {
            foreach (var bundle in _bundles) bundle.Dispose();
            foreach (var dir in _dirs)
                try { Directory.Delete(dir, recursive: true); } catch { }
        }

        private RocksDbChainStoreBundle OpenBundle(bool pathKeyed)
        {
            var dir = Path.Combine(Path.GetTempPath(), $"flatgen_{Guid.NewGuid():N}");
            _dirs.Add(dir);
            var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: new RocksDbStorageOptions { PathKeyedState = pathKeyed });
            _bundles.Add(bundle);
            return bundle;
        }

        private static byte[] Key(byte leading, byte within = 0, byte last = 0)
        {
            var key = new byte[32];
            key[0] = leading;
            key[1] = within;
            key[31] = last;
            return key;
        }

        private static byte[] SlotKey(int index) => Sha3Keccack.Current.CalculateHash(BitConverter.GetBytes(index));

        private static byte[] StorageRowKey(byte[] owner, byte[] slot) => owner.Concat(slot).ToArray();

        private sealed class IndependentState
        {
            private readonly List<(byte[] Hash, ulong Nonce, ulong Balance, Dictionary<byte[], byte[]> Slots)> _accounts = new();

            public IndependentState Account(byte[] hash, ulong nonce, ulong balance, params (byte[] Slot, byte[] Value)[] slots)
            {
                _accounts.Add((hash, nonce, balance, slots.ToDictionary(s => s.Slot, s => s.Value, ByteArrayComparer.Current)));
                return this;
            }

            public IEnumerable<(byte[] Hash, ulong Nonce, ulong Balance, Dictionary<byte[], byte[]> Slots)> Accounts => _accounts;

            public byte[] StorageRootOf(byte[] hash) => StorageRoot(_accounts.Single(a => ByteUtil.AreEqual(a.Hash, hash)).Slots);

            public byte[] Root()
            {
                var accountTrie = new PatriciaTrie();
                foreach (var account in _accounts)
                    accountTrie.Put(account.Hash, Encode(account.Nonce, account.Balance, StorageRoot(account.Slots)));
                return accountTrie.Root.GetHash();
            }

            private static byte[] StorageRoot(Dictionary<byte[], byte[]> slots)
            {
                if (slots.Count == 0) return DefaultValues.EMPTY_TRIE_HASH;
                var storageTrie = new PatriciaTrie();
                foreach (var slot in slots)
                    storageTrie.Put(slot.Key, RLP.RLP.EncodeElement(slot.Value));
                return storageTrie.Root.GetHash();
            }

            private static byte[] Encode(ulong nonce, ulong balance, byte[] storageRoot)
                => new AccountEncoder().Encode(NewAccount(nonce, balance, storageRoot));
        }

        private static Account NewAccount(ulong nonce, ulong balance, byte[] storageRoot) => new Account
        {
            Nonce = nonce,
            Balance = balance,
            StateRoot = storageRoot,
            CodeHash = DefaultValues.EMPTY_DATA_HASH,
        };

        private static async Task WriteFlatAsync(
            RocksDbChainStoreBundle bundle, IndependentState state, Func<byte[], byte[]> flatStorageRoot = null)
        {
            var flat = new RocksDbStateStore(bundle.Rocks);
            foreach (var account in state.Accounts)
            {
                var storageRoot = flatStorageRoot?.Invoke(account.Hash) ?? state.StorageRootOf(account.Hash);
                await flat.SaveAccountByHashAsync(account.Hash, NewAccount(account.Nonce, account.Balance, storageRoot));
                foreach (var slot in account.Slots)
                    await flat.SaveStorageByHashAsync(account.Hash, slot.Key, slot.Value);
            }
        }

        private static Task<Account> ReadFlatAccountAsync(RocksDbChainStoreBundle bundle, byte[] hash)
            => new RocksDbStateStore(bundle.Rocks).GetAccountByHashAsync(hash);

        [Fact]
        public async Task Given_FlatStateWithStaleStorageRootsAfterCatchUp_When_TheTrieIsGeneratedFromFlat_Then_TheRootMatchesAndTheStaleFlatRootsAreRewritten()
        {
            var bundle = OpenBundle(pathKeyed: true);
            var staleContract = Key(0x20, 1);
            var createdContract = Key(0x90, 2);
            var consistentContract = Key(0xB0, 3);
            var state = new IndependentState()
                .Account(staleContract, 1, 10, (SlotKey(1), new byte[] { 0x09 }), (SlotKey(2), new byte[] { 0x0a }))
                .Account(Key(0x50, 4), 2, 20)
                .Account(createdContract, 1, 0, (SlotKey(3), new byte[] { 0x0b }))
                .Account(consistentContract, 3, 30, (SlotKey(1), new byte[] { 0x33 }))
                .Account(Key(0xff, 0xff, 0xff), 4, 40);
            var staleRoot = new IndependentState().Account(staleContract, 1, 10, (SlotKey(1), new byte[] { 0x01 })).StorageRootOf(staleContract);
            await WriteFlatAsync(bundle, state, hash =>
                ByteUtil.AreEqual(hash, staleContract) ? staleRoot
                : ByteUtil.AreEqual(hash, createdContract) ? DefaultValues.EMPTY_TRIE_HASH
                : null);
            var expectedRoot = state.Root();

            var result = await bundle.GenerateTrieFromFlatAsync(expectedRoot, null, CancellationToken.None);

            Assert.Equal(expectedRoot, result.Root);
            Assert.Equal(5, result.AccountsScanned);
            Assert.Equal(4, result.SlotsScanned);
            Assert.Equal(2, result.AccountRootsRewritten);
            Assert.Equal(0, result.DanglingSlotsDeleted);
            Assert.Equal(state.StorageRootOf(staleContract), (await ReadFlatAccountAsync(bundle, staleContract)).StateRoot);
            Assert.Equal(state.StorageRootOf(createdContract), (await ReadFlatAccountAsync(bundle, createdContract)).StateRoot);
            var verify = await bundle.VerifyFlatStateAsync(expectedRoot, null, CancellationToken.None);
            Assert.Equal(0, verify.AccountsPatched);
            Assert.Equal(0, verify.TotalRepairs);
        }

        [Fact]
        public async Task Given_StorageRowsWhoseOwnerHasNoAccountRow_When_Generated_Then_TheyAreDeleted()
        {
            var bundle = OpenBundle(pathKeyed: true);
            var liveOwner = Key(0x31);
            var state = new IndependentState()
                .Account(liveOwner, 1, 1, (SlotKey(1), new byte[] { 0x01 }), (SlotKey(2), new byte[] { 0x02 }))
                .Account(Key(0x38), 1, 2);
            await WriteFlatAsync(bundle, state);
            var flat = new RocksDbStateStore(bundle.Rocks);
            var danglingOwners = new[] { Key(0x30), Key(0x35), Key(0x3f), Key(0x90), Key(0xff, 0xff, 0xff) };
            foreach (var owner in danglingOwners)
                await flat.SaveStorageByHashAsync(owner, SlotKey(7), new byte[] { 0x07 });
            var expectedRoot = state.Root();

            var result = await bundle.GenerateTrieFromFlatAsync(expectedRoot, null, CancellationToken.None);

            Assert.Equal(expectedRoot, result.Root);
            Assert.Equal(danglingOwners.Length, result.DanglingSlotsDeleted);
            foreach (var owner in danglingOwners)
                Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, StorageRowKey(owner, SlotKey(7))));
            Assert.NotNull(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, StorageRowKey(liveOwner, SlotKey(1))));
            Assert.NotNull(bundle.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, StorageRowKey(liveOwner, SlotKey(2))));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Given_AWhaleLargerThanTheCollapseInterval_When_GeneratedWithCollapseEveryOneSlotAndWithNoCollapse_Then_BothRootsEqualAnIndependentlyBuiltTrie(bool pathKeyed)
        {
            var bundle = OpenBundle(pathKeyed);
            var whaleSlots = Enumerable.Range(0, 600)
                .Select(i => (SlotKey(i), new byte[] { (byte)(i % 250 + 1), (byte)(i / 250) }))
                .ToArray();
            var state = new IndependentState().Account(Key(0x77, 7), 1, 1, whaleSlots);
            for (int i = 0; i < 48; i++)
                state.Account(Key((byte)(i * 5), (byte)i, 0x42), (ulong)i + 1, (ulong)i * 3);
            await WriteFlatAsync(bundle, state);
            var expectedRoot = state.Root();
            var flat = new RocksDbStateStore(bundle.Rocks);

            var collapsedEverySlot = await new FlatStateTrieGenerator(
                    bundle.Rocks, flat, bundle.StateTrieNodes, storageCollapseIntervalSlots: 1, accountCollapseIntervalAccounts: 1)
                .GenerateAsync(expectedRoot, null, CancellationToken.None);
            var neverCollapsed = await new FlatStateTrieGenerator(
                    bundle.Rocks, flat, bundle.StateTrieNodes, storageCollapseIntervalSlots: int.MaxValue, accountCollapseIntervalAccounts: int.MaxValue)
                .GenerateAsync(expectedRoot, null, CancellationToken.None);

            Assert.Equal(expectedRoot, collapsedEverySlot.Root);
            Assert.Equal(expectedRoot, neverCollapsed.Root);
            Assert.Equal(600, collapsedEverySlot.SlotsScanned);
            Assert.Equal(49, collapsedEverySlot.AccountsScanned);
        }

        [Fact]
        public async Task Given_LeftoverTrieNodesAtUnusedPaths_When_GeneratedOnAPathKeyedStore_Then_TheTrieColumnFamiliesWereWipedFirst()
        {
            var state = new IndependentState()
                .Account(Key(0x12), 1, 1, (SlotKey(1), new byte[] { 0x01 }))
                .Account(Key(0xc4), 2, 2);
            var junkAccountPath = new byte[] { 0x0a, 0x0b, 0x0c, 0x0d, 0x0e };
            var junkStoragePath = StorageRowKey(Key(0xee), Array.Empty<byte>()).Concat(new byte[] { 0x01, 0x02, 0x03 }).ToArray();
            var junkHashNode = Sha3Keccack.Current.CalculateHash(new byte[] { 0xde, 0xad });
            var junk = Enumerable.Repeat((byte)0x5a, 40).ToArray();

            var pathBundle = OpenBundle(pathKeyed: true);
            await WriteFlatAsync(pathBundle, state);
            pathBundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_ACCOUNT, junkAccountPath, junk);
            pathBundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_STORAGE, junkStoragePath, junk);

            var pathResult = await pathBundle.GenerateTrieFromFlatAsync(state.Root(), null, CancellationToken.None);

            Assert.Equal(state.Root(), pathResult.Root);
            Assert.Null(pathBundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, junkAccountPath));
            Assert.Null(pathBundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, junkStoragePath));

            var hashBundle = OpenBundle(pathKeyed: false);
            await WriteFlatAsync(hashBundle, state);
            hashBundle.Rocks.Put(RocksDbManager.CF_TRIE_NODES, junkHashNode, junk);

            var hashResult = await hashBundle.GenerateTrieFromFlatAsync(state.Root(), null, CancellationToken.None);

            Assert.Equal(state.Root(), hashResult.Root);
            Assert.Equal(junk, hashBundle.Rocks.Get(RocksDbManager.CF_TRIE_NODES, junkHashNode));
        }

        [Fact]
        public async Task Given_AnExpectedRootThatDiffers_When_Generated_Then_ItThrowsNamingBothRootsAndARerunStartsFromAWipedTrie()
        {
            var bundle = OpenBundle(pathKeyed: true);
            var state = new IndependentState()
                .Account(Key(0x05), 1, 1, (SlotKey(1), new byte[] { 0x11 }), (SlotKey(2), new byte[] { 0x22 }))
                .Account(Key(0x66), 2, 2)
                .Account(Key(0xd0), 3, 3, (SlotKey(3), new byte[] { 0x33 }));
            await WriteFlatAsync(bundle, state);
            var expectedRoot = state.Root();
            var wrongRoot = Sha3Keccack.Current.CalculateHash(new byte[] { 0x0b, 0xad });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => bundle.GenerateTrieFromFlatAsync(wrongRoot, null, CancellationToken.None));

            Assert.Contains(wrongRoot.ToHex(), ex.Message);
            Assert.Contains(expectedRoot.ToHex(), ex.Message);

            var junkAccountPath = new byte[] { 0x0f, 0x0e, 0x0d, 0x0c };
            bundle.Rocks.Put(RocksDbManager.CF_STATE_TRIE_ACCOUNT, junkAccountPath, Enumerable.Repeat((byte)0x5a, 40).ToArray());

            var rerun = await bundle.GenerateTrieFromFlatAsync(expectedRoot, null, CancellationToken.None);

            Assert.Equal(expectedRoot, rerun.Root);
            Assert.Equal(3, rerun.AccountsScanned);
            Assert.Equal(3, rerun.SlotsScanned);
            Assert.Equal(0, rerun.AccountRootsRewritten);
            Assert.Equal(0, rerun.DanglingSlotsDeleted);
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, junkAccountPath));
        }
    }
}
