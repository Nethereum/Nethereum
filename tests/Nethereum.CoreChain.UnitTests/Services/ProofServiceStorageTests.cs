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
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.RPC.Eth.Mappers;

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
            var trieNodeStore = new InMemoryContentNodeStore();

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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Given_AnAddressAbsentFromTheStateTrie_When_ItsProofIsGenerated_Then_TheAccountProofIsTheExclusionPathFromTheStateRoot(bool trieBacked)
        {
            const string absentAddress = "0x2222222222222222222222222222222222222222";
            var (stateStore, trieNodeStore, stateRoot) = await SetupStateWithPersistedTrieNodes();
            var proofService = new ProofService(stateStore, trieBacked ? trieNodeStore : null);

            var absent = await proofService.GenerateAccountProofAsync(absentAddress, new List<BigInteger>(), stateRoot);
            var present = await proofService.GenerateAccountProofAsync(AccountAddress, new List<BigInteger>(), stateRoot);

            var absentProof = absent.AccountProofs.Select(p => p.HexToByteArray()).ToList();
            Assert.NotEmpty(absentProof);
            Assert.Equal(stateRoot, _sha3.CalculateHash(absentProof[0]));
            Assert.True(PatriciaProofVerifier.TryVerify(stateRoot, AddressKey(absentAddress), absentProof, out var absentValue));
            Assert.Null(absentValue);
            Assert.True(ProofVerification.Current.Account.Verify(
                stateRoot, absentProof, absentAddress, absent.ToAccount()));
            var claimed = absent.ToAccount();
            claimed.Balance = 1;
            Assert.False(ProofVerification.Current.Account.Verify(stateRoot, absentProof, absentAddress, claimed));

            var presentProof = present.AccountProofs.Select(p => p.HexToByteArray()).ToList();
            Assert.True(PatriciaProofVerifier.TryVerify(stateRoot, AddressKey(AccountAddress), presentProof, out var presentValue));
            EvmUInt256 expectedBalance = 1000;
            Assert.Equal(expectedBalance, AccountEncoder.Current.Decode(presentValue).Balance);
        }

        [Fact]
        public async Task Given_AnAccountWithStorage_When_AnUnsetSlotIsProven_Then_TheStorageProofIsTheExclusionPathFromTheStorageHash()
        {
            var (stateStore, trieNodeStore, stateRoot) = await SetupStateWithPersistedTrieNodes();
            var proofService = new ProofService(stateStore, trieNodeStore);

            var result = await proofService.GenerateAccountProofAsync(
                AccountAddress, new List<BigInteger> { new BigInteger(999), BigInteger.Zero }, stateRoot);

            var storageHash = result.StorageHash.HexToByteArray();
            var unset = result.StorageProof[0];
            var unsetProof = unset.Proof.Select(p => p.HexToByteArray()).ToList();
            Assert.Equal(BigInteger.Zero, unset.Value.Value);
            Assert.NotEmpty(unsetProof);
            Assert.Equal(storageHash, _sha3.CalculateHash(unsetProof[0]));
            Assert.True(PatriciaProofVerifier.TryVerify(storageHash, SlotKey(999), unsetProof, out var unsetValue));
            Assert.Null(unsetValue);
            var slot999 = new BigInteger(999).ToByteArray(isUnsigned: true, isBigEndian: true);
            Assert.True(ProofVerification.Current.Storage.Verify(storageHash, unsetProof, slot999, new byte[] { 0x00 }));
            Assert.False(ProofVerification.Current.Storage.Verify(storageHash, unsetProof, slot999, new byte[] { 0x01 }));

            var setProof = result.StorageProof[1].Proof.Select(p => p.HexToByteArray()).ToList();
            Assert.True(PatriciaProofVerifier.TryVerify(storageHash, SlotKey(0), setProof, out var setValue));
            Assert.Equal(new BigInteger(100), RLP.RLP.Decode(setValue).RLPData.ToBigIntegerFromRLPDecoded());
        }

        [Fact]
        public async Task Given_ATrieStoreMissingANodeBelowARetainedRoot_When_AProofIsRequested_Then_StateNotAvailableIsThrownInsteadOfAnEmptyProof()
        {
            var (stateStore, trieNodeStore, stateRoot) = await SetupStateWithPersistedTrieNodes();
            var rootOnly = new InMemoryContentNodeStore();
            rootOnly.Put(stateRoot, trieNodeStore.Get(stateRoot));
            var proofService = new ProofService(stateStore, rootOnly);

            var missing = await Assert.ThrowsAsync<StateNotAvailableException>(() =>
                proofService.GenerateAccountProofAsync(AccountAddress, new List<BigInteger> { BigInteger.Zero }, stateRoot));

            Assert.Equal(stateRoot, missing.StateRoot);
            Assert.DoesNotContain("is not retained", missing.Message);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Given_AnAddressAbsentFromTheStateTrie_When_ItsProofIsGenerated_Then_CodeHashAndStorageHashAreZero_AsGethReturnsThem(bool trieBacked)
        {
            const string absentAddress = "0x2222222222222222222222222222222222222222";
            var (stateStore, trieNodeStore, stateRoot) = await SetupStateWithPersistedTrieNodes();
            var proofService = new ProofService(stateStore, trieBacked ? trieNodeStore : null);

            var absent = await proofService.GenerateAccountProofAsync(absentAddress, new List<BigInteger> { BigInteger.One }, stateRoot);
            var present = await proofService.GenerateAccountProofAsync(AccountAddress, new List<BigInteger>(), stateRoot);

            var zeroHash = new byte[32].ToHex(true);
            Assert.Equal(zeroHash, absent.CodeHash);
            Assert.Equal(zeroHash, absent.StorageHash);
            Assert.Equal(BigInteger.Zero, absent.StorageProof[0].Value.Value);
            Assert.Empty(absent.StorageProof[0].Proof);
            Assert.Equal(DefaultValues.EMPTY_DATA_HASH.ToHex(true), present.CodeHash);
            Assert.NotEqual(zeroHash, present.StorageHash);
        }

        private byte[] AddressKey(string address)
            => _sha3.CalculateHash(AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray());

        private byte[] SlotKey(int slot)
            => _sha3.CalculateHash(new BigInteger(slot).ToBytesForRLPEncoding().PadBytes(32));

        private async Task<(InMemoryStateStore, InMemoryContentNodeStore, byte[])> SetupStateWithPersistedTrieNodes()
        {
            var stateStore = new InMemoryStateStore();
            var trieNodeStore = new InMemoryContentNodeStore();

            await stateStore.SaveStorageAsync(AccountAddress, BigInteger.Zero, BigInteger.Parse("100").ToByteArray(isUnsigned: true, isBigEndian: true));
            await stateStore.SaveStorageAsync(AccountAddress, BigInteger.One, BigInteger.Parse("200").ToByteArray(isUnsigned: true, isBigEndian: true));
            await stateStore.SaveStorageAsync(AccountAddress, new BigInteger(2), BigInteger.Parse("300").ToByteArray(isUnsigned: true, isBigEndian: true));

            var storageSlots = await stateStore.GetAllStorageAsync(AccountAddress);
            var storageRoot = _rootCalculator.CalculateStorageRoot(storageSlots, trieNodeStore);

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

        private byte[] ComputeAndPersistStateRoot(InMemoryStateStore stateStore, InMemoryContentNodeStore trieNodeStore)
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
            public System.Collections.Generic.IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => _inner.StreamAccountsAsync();
            public Task<byte[]> GetStorageAsync(string address, BigInteger slot) => _inner.GetStorageAsync(address, slot);
            public Task SaveStorageAsync(string address, BigInteger slot, byte[] value) => _inner.SaveStorageAsync(address, slot, value);
            public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value) => _inner.SaveStorageByKeccakAsync(address, slotKeccak, value);
            public Task ClearStorageAsync(string address) => _inner.ClearStorageAsync(address);
            public Task<byte[]> GetCodeAsync(byte[] codeHash) => _inner.GetCodeAsync(codeHash);
            public Task SaveCodeAsync(byte[] codeHash, byte[] code) => _inner.SaveCodeAsync(codeHash, code);
            public Task<IStateSnapshot> CreateSnapshotAsync() => _inner.CreateSnapshotAsync();
            public Task CommitSnapshotAsync(IStateSnapshot snapshot) => _inner.CommitSnapshotAsync(snapshot);
            public Task RevertSnapshotAsync(IStateSnapshot snapshot) => _inner.RevertSnapshotAsync(snapshot);
            public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => _inner.GetDirtyAccountAddressesAsync();
            public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address) => _inner.GetDirtyStorageSlotsAsync(address);
            public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync() => _inner.GetStorageClearedAddressesAsync();
            public Task ClearDirtyTrackingAsync() => _inner.ClearDirtyTrackingAsync();

            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address)
            {
                GetAllStorageCallCount++;
                return _inner.GetAllStorageAsync(address);
            }
        }
    }
}
