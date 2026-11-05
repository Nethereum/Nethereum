using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Binary.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class StateRootWideScanPinTests
    {
        private const string MixedCaseAddress = "0xAbCdEf0123456789aBcDeF0123456789AbCdEf01";
        private const string OtherAddress = "0x1000000000000000000000000000000000000001";

        private static async Task<InMemoryStateStore> StoreWithAccountsAsync()
        {
            var store = new InMemoryStateStore();
            await store.SaveAccountAsync(MixedCaseAddress, new Account { Balance = 5, Nonce = 1 });
            await store.SaveAccountAsync(OtherAddress, new Account { Balance = 9, Nonce = 2 });
            await store.SaveStorageAsync(OtherAddress, 0, new byte[] { 0x00, 0x00, 0x07 });
            await store.SaveStorageAsync(OtherAddress, 3, new byte[] { 0x42 });
            return store;
        }

        [Fact]
        public async Task Given_NullAllZeroOrEmptyPreviousStateRoot_When_InitialisingBinaryIncremental_Then_StartsFromEmptyTrie()
        {
            var roots = new List<byte[]>();
            foreach (var previous in new byte[][] { null, new byte[32], new byte[0], new byte[] { 0, 0 } })
            {
                var store = await StoreWithAccountsAsync();
                var calculator = new BinaryIncrementalStateRootCalculator(store, null, new InMemoryBinaryTrieStorage());
                roots.Add(await calculator.ComputeStateRootAsync(previous));
            }

            Assert.NotEqual(new byte[32], roots[0]);
            foreach (var root in roots) Assert.Equal(roots[0], root);
        }

        [Fact]
        public async Task Given_EmptyOrAllZeroValue_When_SlotWritten_Then_SlotIsNotInTrie()
        {
            var withNoSlot = new InMemoryStateStore();
            await withNoSlot.SaveAccountAsync(OtherAddress, new Account { Balance = 9, Nonce = 2 });
            var withZeroSlots = new InMemoryStateStore();
            await withZeroSlots.SaveAccountAsync(OtherAddress, new Account { Balance = 9, Nonce = 2 });
            await withZeroSlots.SaveStorageAsync(OtherAddress, 1, new byte[] { 0x05 });
            await withZeroSlots.SaveStorageAsync(OtherAddress, 1, new byte[] { 0x00, 0x00 });
            await withZeroSlots.SaveStorageAsync(OtherAddress, 2, new byte[0]);
            await withZeroSlots.SaveStorageAsync(OtherAddress, 3, null);

            var baseline = await new BinaryIncrementalStateRootCalculator(withNoSlot).ComputeStateRootAsync();
            var zeroed = await new BinaryIncrementalStateRootCalculator(withZeroSlots).ComputeStateRootAsync();
            var patriciaBaseline = await new StateRootCalculator().ComputeStateRootAsync(withNoSlot);
            var patriciaZeroed = await new StateRootCalculator().ComputeStateRootAsync(withZeroSlots);

            Assert.Equal(baseline, zeroed);
            Assert.Equal(patriciaBaseline, patriciaZeroed);
            Assert.Empty(await withZeroSlots.GetAllStorageAsync(OtherAddress));
        }

        [Fact]
        public async Task Given_TheSameAccountsAndStorage_When_TheSyncAndAsyncOverloadsComputeTheRoot_Then_RootsAndPersistedNodesAreEqual()
        {
            var store = await StoreWithAccountsAsync();
            var asyncNodes = new InMemoryContentNodeStore();
            var asyncRoot = await new StateRootCalculator().ComputeStateRootAsync(store, asyncNodes);

            var accounts = new Dictionary<string, Account>
            {
                [MixedCaseAddress] = new Account { Balance = 5, Nonce = 1 },
                [OtherAddress] = new Account { Balance = 9, Nonce = 2 }
            };
            var storage = new Dictionary<string, IDictionary<BigInteger, byte[]>>
            {
                [OtherAddress] = new Dictionary<BigInteger, byte[]>
                {
                    [0] = new byte[] { 0x00, 0x00, 0x07 },
                    [3] = new byte[] { 0x42 },
                    [4] = new byte[] { 0x00 }
                }
            };
            var syncNodes = new InMemoryContentNodeStore();
            var syncRoot = new StateRootCalculator().ComputeStateRoot(accounts, storage, syncNodes);
            var noStoreRoot = new StateRootCalculator().ComputeStateRoot(accounts, storage);

            Assert.Equal(asyncRoot, syncRoot);
            Assert.Equal(asyncRoot, noStoreRoot);
            Assert.NotNull(asyncNodes.Get(asyncRoot));
            Assert.NotNull(syncNodes.Get(syncRoot));
            Assert.Equal(asyncNodes.Count, syncNodes.Count);
        }

        [Fact]
        public void Given_NoAccounts_When_TheSyncOverloadComputesTheRoot_Then_TheEmptyTrieHashIsReturned()
        {
            var calculator = new StateRootCalculator();

            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, calculator.ComputeStateRoot(new Dictionary<string, Account>()));
            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, calculator.ComputeStateRoot(null));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Given_MixedCaseAddress_When_ProvingAccount_Then_ProofKeyEqualsStateKeysAccountKey(bool withNodeStore)
        {
            var store = await StoreWithAccountsAsync();
            var nodes = new InMemoryContentNodeStore();
            var root = await new StateRootCalculator().ComputeStateRootAsync(store, nodes);
            var proofService = new ProofService(store, withNodeStore ? nodes : null);

            var mixed = await proofService.GenerateAccountProofAsync(MixedCaseAddress, new List<BigInteger>(), root);
            var lower = await proofService.GenerateAccountProofAsync(
                MixedCaseAddress.ToLowerInvariant(), new List<BigInteger>(), root);

            Assert.NotEmpty(mixed.AccountProofs);
            Assert.Equal(lower.AccountProofs, mixed.AccountProofs);
            var proofNodes = mixed.AccountProofs.Select(p => p.HexToByteArray()).ToList();
            var verified = ProofVerification.Current.Account.Verify(
                root, proofNodes, MixedCaseAddress,
                new Account
                {
                    Balance = mixed.Balance.Value,
                    Nonce = mixed.Nonce.Value,
                    CodeHash = mixed.CodeHash.HexToByteArray(),
                    StateRoot = mixed.StorageHash.HexToByteArray()
                });
            Assert.True(verified);
            var trie = PatriciaTrie.LoadFromStorage(root, nodes);
            Assert.NotNull(trie.Get(StateKeys.AccountKey(MixedCaseAddress)));
        }

        [Fact]
        public async Task Given_OtherAccountProof_When_StorageSlotsRequested_Then_StorageProofsUseTheStorageOwnerKey()
        {
            var store = await StoreWithAccountsAsync();
            var nodes = new InMemoryContentNodeStore();
            var root = await new StateRootCalculator().ComputeStateRootAsync(store, nodes);

            var proof = await new ProofService(store, nodes).GenerateAccountProofAsync(
                OtherAddress.ToUpperInvariant().Replace("0X", "0x"), new List<BigInteger> { 0, 3 }, root);

            Assert.Equal(2, proof.StorageProof.Count);
            Assert.Equal(new BigInteger(7), proof.StorageProof[0].Value.Value);
            Assert.Equal(new BigInteger(0x42), proof.StorageProof[1].Value.Value);
            Assert.All(proof.StorageProof, p => Assert.NotEmpty(p.Proof));
        }
    }
}
