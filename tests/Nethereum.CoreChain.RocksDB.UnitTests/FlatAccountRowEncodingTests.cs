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
    public class FlatAccountRowEncodingTests : IDisposable
    {
        private readonly List<string> _dirs = new();
        private readonly List<RocksDbChainStoreBundle> _bundles = new();

        public void Dispose()
        {
            foreach (var bundle in _bundles) bundle.Dispose();
            foreach (var dir in _dirs)
                try { Directory.Delete(dir, recursive: true); } catch { }
        }

        private RocksDbChainStoreBundle OpenPathKeyedBundle()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"flatleaf_{Guid.NewGuid():N}");
            _dirs.Add(dir);
            var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: new RocksDbStorageOptions { PathKeyedState = true });
            _bundles.Add(bundle);
            return bundle;
        }

        private static byte[] Key(byte leading, byte within = 0)
        {
            var key = new byte[32];
            key[0] = leading;
            key[1] = within;
            return key;
        }

        private static byte[] SlotKey(int index) => Sha3Keccack.Current.CalculateHash(BitConverter.GetBytes(index));

        private static Account Eoa(ulong nonce, ulong balance, byte[] storageRoot, byte[] codeHash) => new Account
        {
            Nonce = nonce,
            Balance = balance,
            StateRoot = storageRoot,
            CodeHash = codeHash,
        };

        private static Account CanonicalEoa(ulong nonce, ulong balance)
            => Eoa(nonce, balance, DefaultValues.EMPTY_TRIE_HASH, DefaultValues.EMPTY_DATA_HASH);

        private static byte[] ContractStorageRoot(byte[] slot, byte[] value)
        {
            var trie = new PatriciaTrie();
            trie.Put(slot, RLP.RLP.EncodeElement(value));
            return trie.Root.GetHash();
        }

        private static byte[] IndependentRoot(params (byte[] Hash, Account Account)[] accounts)
        {
            var trie = new PatriciaTrie();
            foreach (var account in accounts)
                trie.Put(account.Hash, new AccountEncoder().Encode(account.Account));
            return trie.Root.GetHash();
        }

        private static byte[] UndecodableAccountRow()
        {
            var truncatedAccountList = RLP.RLP.EncodeList(RLP.RLP.EncodeElement(new byte[] { 0x01 }), RLP.RLP.EncodeElement(new byte[] { 0x02 }));
            return new byte[20].Concat(truncatedAccountList).ToArray();
        }

        public static IEnumerable<object[]> Layouts()
        {
            yield return new object[] { "rlp" };
            yield return new object[] { "binary" };
        }

        private static RocksDbStateStore FlatStore(RocksDbChainStoreBundle bundle, string layout)
            => layout == "binary"
                ? new RocksDbStateStore(bundle.Rocks, accountLayout: BinaryPackedAccountLayout.Instance)
                : new RocksDbStateStore(bundle.Rocks);

        [Theory]
        [MemberData(nameof(Layouts))]
        public async Task Given_AnAccountWithANullStateRootAndCodeHashInFlat_When_TheTrieIsGeneratedFromFlat_Then_ItHashesAsEmptyAndTheRootMatches(string layout)
        {
            var bundle = OpenPathKeyedBundle();
            var flat = FlatStore(bundle, layout);
            var eoa = Key(0x21, 1);
            var contract = Key(0xa4, 2);
            var slot = SlotKey(1);
            var storageRoot = ContractStorageRoot(slot, new byte[] { 0x07 });
            await flat.SaveAccountByHashAsync(eoa, Eoa(3, 30, null, null));
            await flat.SaveAccountByHashAsync(contract, Eoa(1, 10, storageRoot, DefaultValues.EMPTY_DATA_HASH));
            await flat.SaveStorageByHashAsync(contract, slot, new byte[] { 0x07 });
            var expectedRoot = IndependentRoot(
                (eoa, CanonicalEoa(3, 30)),
                (contract, Eoa(1, 10, storageRoot, DefaultValues.EMPTY_DATA_HASH)));

            var result = await new FlatStateTrieGenerator(bundle.Rocks, flat, bundle.StateTrieNodes)
                .GenerateAsync(expectedRoot, null, CancellationToken.None);

            Assert.Equal(expectedRoot, result.Root);
            Assert.Equal(2, result.AccountsScanned);
            Assert.Equal(layout == "binary" ? 1 : 0, result.AccountRootsRewritten);
        }

        [Fact]
        public async Task Given_AnAccountWithANullStateRootAndCodeHashInFlat_When_Certified_Then_ItHashesAsEmptyAndTheRootMatches()
        {
            var bundle = OpenPathKeyedBundle();
            var flat = new RocksDbStateStore(bundle.Rocks);
            var eoa = Key(0x21, 1);
            var other = Key(0xc0, 9);
            await flat.SaveAccountByHashAsync(eoa, CanonicalEoa(3, 30));
            await flat.SaveAccountByHashAsync(other, CanonicalEoa(4, 40));
            var root = IndependentRoot((eoa, CanonicalEoa(3, 30)), (other, CanonicalEoa(4, 40)));
            await bundle.GenerateTrieFromFlatAsync(root, null, CancellationToken.None);
            await flat.SaveAccountByHashAsync(eoa, Eoa(3, 30, null, null));

            var result = await bundle.ReconcileFlatStateAsync(root, null, CancellationToken.None);

            Assert.Equal(2, result.AccountsScanned);
            Assert.Equal(0, result.AccountsPatched);
            Assert.Equal(0, result.TotalRepairs);
            Assert.Equal(2, result.RangesWalked);
        }

        [Fact]
        public async Task Given_AFlatAccountRowWithADifferentBalance_When_Reconciled_Then_ItIsPatchedFromTheTrie()
        {
            var bundle = OpenPathKeyedBundle();
            var flat = new RocksDbStateStore(bundle.Rocks);
            var eoa = Key(0x21, 1);
            await flat.SaveAccountByHashAsync(eoa, CanonicalEoa(3, 30));
            var root = IndependentRoot((eoa, CanonicalEoa(3, 30)));
            await bundle.GenerateTrieFromFlatAsync(root, null, CancellationToken.None);
            await flat.SaveAccountByHashAsync(eoa, Eoa(3, 31, null, null));

            var result = await bundle.ReconcileFlatStateAsync(root, null, CancellationToken.None);

            Assert.Equal(1, result.AccountsPatched);
            Assert.Equal((EvmUInt256)30, (await flat.GetAccountByHashAsync(eoa)).Balance);
        }

        [Fact]
        public async Task Given_AFlatAccountRowThatDoesNotDecode_When_Certified_Then_ItIsAMismatchPatchedFromTheTrieAndNothingThrows()
        {
            var bundle = OpenPathKeyedBundle();
            var flat = new RocksDbStateStore(bundle.Rocks);
            var broken = Key(0x52, 5);
            var other = Key(0xe1, 6);
            await flat.SaveAccountByHashAsync(broken, CanonicalEoa(7, 70));
            await flat.SaveAccountByHashAsync(other, CanonicalEoa(8, 80));
            var root = IndependentRoot((broken, CanonicalEoa(7, 70)), (other, CanonicalEoa(8, 80)));
            await bundle.GenerateTrieFromFlatAsync(root, null, CancellationToken.None);
            bundle.Rocks.Put(RocksDbManager.CF_STATE_ACCOUNTS, broken, UndecodableAccountRow());

            var result = await bundle.ReconcileFlatStateAsync(root, null, CancellationToken.None);

            Assert.Equal(2, result.AccountsScanned);
            Assert.Equal(1, result.AccountsPatched);
            Assert.Equal(1, result.TotalRepairs);
            Assert.Equal(2, result.RangesWalked);
            var repaired = await flat.GetAccountByHashAsync(broken);
            Assert.Equal((EvmUInt256)7, repaired.Nonce);
            Assert.Equal((EvmUInt256)70, repaired.Balance);
            var again = await bundle.ReconcileFlatStateAsync(root, null, CancellationToken.None);
            Assert.Equal(0, again.TotalRepairs);
        }

        [Fact]
        public async Task Given_AFlatAccountRowShorterThanAnAddressPrefix_When_Reconciled_Then_ItIsAMismatchPatchedFromTheTrieAndNothingThrows()
        {
            var bundle = OpenPathKeyedBundle();
            var flat = new RocksDbStateStore(bundle.Rocks);
            var truncated = Key(0x52, 5);
            var other = Key(0xe1, 6);
            await flat.SaveAccountByHashAsync(truncated, CanonicalEoa(7, 70));
            await flat.SaveAccountByHashAsync(other, CanonicalEoa(8, 80));
            var root = IndependentRoot((truncated, CanonicalEoa(7, 70)), (other, CanonicalEoa(8, 80)));
            await bundle.GenerateTrieFromFlatAsync(root, null, CancellationToken.None);
            bundle.Rocks.Put(RocksDbManager.CF_STATE_ACCOUNTS, truncated, new byte[] { 0x01, 0x02, 0x03 });

            var result = await bundle.ReconcileFlatStateAsync(root, null, CancellationToken.None);

            Assert.Equal(2, result.AccountsScanned);
            Assert.Equal(1, result.AccountsPatched);
            Assert.Equal(0, result.AccountsAdded);
            Assert.Equal(1, result.TotalRepairs);
            var repaired = await flat.GetAccountByHashAsync(truncated);
            Assert.Equal((EvmUInt256)7, repaired.Nonce);
            Assert.Equal((EvmUInt256)70, repaired.Balance);
        }

        [Fact]
        public async Task Given_AFlatAccountRowThatDoesNotDecode_When_TheTrieIsGeneratedFromFlat_Then_ItFailsNamingTheAccount()
        {
            var bundle = OpenPathKeyedBundle();
            var flat = new RocksDbStateStore(bundle.Rocks);
            var broken = Key(0x52, 5);
            await flat.SaveAccountByHashAsync(Key(0x10), CanonicalEoa(1, 1));
            bundle.Rocks.Put(RocksDbManager.CF_STATE_ACCOUNTS, broken, UndecodableAccountRow());

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => bundle.GenerateTrieFromFlatAsync(new byte[32], null, CancellationToken.None));

            Assert.Contains(broken.ToHex(), ex.Message);
        }
    }
}
