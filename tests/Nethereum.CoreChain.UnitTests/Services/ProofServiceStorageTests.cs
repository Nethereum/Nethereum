using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Services
{
    public class ProofServiceStorageTests
    {
        private readonly Sha3Keccack _sha3 = new();
        private readonly RootCalculator _rootCalculator = new();

        private const string AccountAddress = "0x1111111111111111111111111111111111111111";

        [Fact]
        public async Task StorageProof_FastPath_ReturnsNonEmptyProofs()
        {
            var (stateStore, trieNodeStore, stateRoot) = await SetupStateWithPersistedTrieNodes();

            var proofService = new ProofService(stateStore, trieNodeStore);
            var result = await proofService.GenerateAccountProofAsync(
                AccountAddress,
                new List<BigInteger> { BigInteger.Zero, BigInteger.One, new BigInteger(2) },
                stateRoot);

            Assert.NotNull(result);
            Assert.Equal(3, result.StorageProof.Count);

            foreach (var sp in result.StorageProof)
            {
                Assert.NotNull(sp.Proof);
                Assert.NotEmpty(sp.Proof);
            }
        }

        [Fact]
        public async Task StorageProof_FastPath_ReturnsCorrectValues()
        {
            var (stateStore, trieNodeStore, stateRoot) = await SetupStateWithPersistedTrieNodes();

            var proofService = new ProofService(stateStore, trieNodeStore);
            var result = await proofService.GenerateAccountProofAsync(
                AccountAddress,
                new List<BigInteger> { BigInteger.Zero, BigInteger.One, new BigInteger(2) },
                stateRoot);

            Assert.Equal(new BigInteger(100), result.StorageProof[0].Value.Value);
            Assert.Equal(new BigInteger(200), result.StorageProof[1].Value.Value);
            Assert.Equal(new BigInteger(300), result.StorageProof[2].Value.Value);
        }

        [Fact]
        public async Task StorageProof_FastPath_DoesNotCallGetAllStorage()
        {
            var (stateStore, trieNodeStore, stateRoot) = await SetupStateWithPersistedTrieNodes();

            var trackingStore = new GetAllStorageTrackingStore(stateStore);
            var proofService = new ProofService(trackingStore, trieNodeStore);

            await proofService.GenerateAccountProofAsync(
                AccountAddress,
                new List<BigInteger> { BigInteger.Zero },
                stateRoot);

            Assert.Equal(0, trackingStore.GetAllStorageCallCount);
        }

        [Fact]
        public async Task StorageProof_FallbackPath_UsedWhenNoTrieNodeStore()
        {
            var (stateStore, _, stateRoot) = await SetupStateWithPersistedTrieNodes();

            var proofService = new ProofService(stateStore, trieNodeStore: null);
            var result = await proofService.GenerateAccountProofAsync(
                AccountAddress,
                new List<BigInteger> { BigInteger.Zero },
                stateRoot);

            Assert.NotNull(result);
            Assert.Single(result.StorageProof);
            Assert.Equal(new BigInteger(100), result.StorageProof[0].Value.Value);
        }

        [Fact]
        public async Task StorageProof_FallbackPath_UsedWhenEmptyTrieHash()
        {
            var stateStore = new InMemoryStateStore();
            var trieNodeStore = new InMemoryTrieNodeStore();

            var account = new Account
            {
                Balance = 1000,
                Nonce = 1,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH
            };
            await stateStore.SaveAccountAsync(AccountAddress, account);

            var stateRoot = ComputeAndPersistStateRoot(stateStore, trieNodeStore);

            var proofService = new ProofService(stateStore, trieNodeStore);
            var result = await proofService.GenerateAccountProofAsync(
                AccountAddress,
                new List<BigInteger> { BigInteger.Zero },
                stateRoot);

            Assert.NotNull(result);
            Assert.Single(result.StorageProof);
            Assert.Equal(BigInteger.Zero, result.StorageProof[0].Value.Value);
            Assert.Empty(result.StorageProof[0].Proof);
        }

        [Fact]
        public async Task StorageHash_FastPath_UsesAccountStateRoot()
        {
            var (stateStore, trieNodeStore, stateRoot) = await SetupStateWithPersistedTrieNodes();

            var trackingStore = new GetAllStorageTrackingStore(stateStore);
            var proofService = new ProofService(trackingStore, trieNodeStore);

            var result = await proofService.GenerateAccountProofAsync(
                AccountAddress,
                new List<BigInteger>(),
                stateRoot);

            Assert.NotNull(result.StorageHash);
            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH.ToHex(true), result.StorageHash);
            Assert.Equal(0, trackingStore.GetAllStorageCallCount);
        }

        [Fact]
        public async Task StorageProof_NonExistentSlot_ReturnsZeroValue()
        {
            var (stateStore, trieNodeStore, stateRoot) = await SetupStateWithPersistedTrieNodes();

            var proofService = new ProofService(stateStore, trieNodeStore);
            var result = await proofService.GenerateAccountProofAsync(
                AccountAddress,
                new List<BigInteger> { new BigInteger(999) },
                stateRoot);

            Assert.Single(result.StorageProof);
            Assert.Equal(BigInteger.Zero, result.StorageProof[0].Value.Value);
        }

        private async Task<(InMemoryStateStore, InMemoryTrieNodeStore, byte[])> SetupStateWithPersistedTrieNodes()
        {
            var stateStore = new InMemoryStateStore();
            var trieNodeStore = new InMemoryTrieNodeStore();

            await stateStore.SaveStorageAsync(AccountAddress, BigInteger.Zero, BigInteger.Parse("100").ToByteArray(isUnsigned: true, isBigEndian: true));
            await stateStore.SaveStorageAsync(AccountAddress, BigInteger.One, BigInteger.Parse("200").ToByteArray(isUnsigned: true, isBigEndian: true));
            await stateStore.SaveStorageAsync(AccountAddress, new BigInteger(2), BigInteger.Parse("300").ToByteArray(isUnsigned: true, isBigEndian: true));

            var storageSlots = await stateStore.GetAllStorageAsync(AccountAddress);
            var storageDict = new Dictionary<byte[], byte[]>();
            foreach (var kvp in storageSlots)
            {
                var slotBytes = kvp.Key.ToBytesForRLPEncoding().PadBytes(32);
                var hashedSlot = _sha3.CalculateHash(slotBytes);
                storageDict[hashedSlot] = kvp.Value;
            }
            var storageRoot = _rootCalculator.CalculateStorageRoot(storageDict, trieNodeStore);

            var account = new Account
            {
                Balance = 1000,
                Nonce = 1,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH
            };
            await stateStore.SaveAccountAsync(AccountAddress, account);

            var stateRoot = ComputeAndPersistStateRoot(stateStore, trieNodeStore);

            return (stateStore, trieNodeStore, stateRoot);
        }

        private byte[] ComputeAndPersistStateRoot(InMemoryStateStore stateStore, InMemoryTrieNodeStore trieNodeStore)
        {
            var accounts = stateStore.GetAllAccountsAsync().Result;
            var accountDict = new Dictionary<byte[], Account>(new ByteArrayComparer());

            foreach (var kvp in accounts)
            {
                var addrBytes = AddressUtil.Current.ConvertToValid20ByteAddress(kvp.Key).HexToByteArray();
                var hashedAddr = _sha3.CalculateHash(addrBytes);
                accountDict[hashedAddr] = kvp.Value;
            }

            return _rootCalculator.CalculateStateRoot(accountDict, trieNodeStore);
        }

        private class GetAllStorageTrackingStore : IStateStore
        {
            private readonly IStateStore _inner;
            public int GetAllStorageCallCount { get; private set; }

            public GetAllStorageTrackingStore(IStateStore inner) => _inner = inner;

            public Task<Account> GetAccountAsync(string address) => _inner.GetAccountAsync(address);
            public Task SaveAccountAsync(string address, Account account) => _inner.SaveAccountAsync(address, account);
            public Task<bool> AccountExistsAsync(string address) => _inner.AccountExistsAsync(address);
            public Task DeleteAccountAsync(string address) => _inner.DeleteAccountAsync(address);
            public Task<Dictionary<string, Account>> GetAllAccountsAsync() => _inner.GetAllAccountsAsync();
            public Task<byte[]> GetStorageAsync(string address, BigInteger slot) => _inner.GetStorageAsync(address, slot);
            public Task SaveStorageAsync(string address, BigInteger slot, byte[] value) => _inner.SaveStorageAsync(address, slot, value);
            public Task ClearStorageAsync(string address) => _inner.ClearStorageAsync(address);
            public Task<byte[]> GetCodeAsync(byte[] codeHash) => _inner.GetCodeAsync(codeHash);
            public Task SaveCodeAsync(byte[] codeHash, byte[] code) => _inner.SaveCodeAsync(codeHash, code);
            public Task<IStateSnapshot> CreateSnapshotAsync() => _inner.CreateSnapshotAsync();
            public Task CommitSnapshotAsync(IStateSnapshot snapshot) => _inner.CommitSnapshotAsync(snapshot);
            public Task RevertSnapshotAsync(IStateSnapshot snapshot) => _inner.RevertSnapshotAsync(snapshot);
            public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => _inner.GetDirtyAccountAddressesAsync();
            public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address) => _inner.GetDirtyStorageSlotsAsync(address);
            public Task ClearDirtyTrackingAsync() => _inner.ClearDirtyTrackingAsync();

            public Task<Dictionary<BigInteger, byte[]>> GetAllStorageAsync(string address)
            {
                GetAllStorageCallCount++;
                return _inner.GetAllStorageAsync(address);
            }
        }
    }
}
