using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class DeferredStorageDebtFinalRootResolverTests
    {
        private static readonly Sha3Keccack Keccak = new();
        private static readonly IHashProvider HashProvider = new Sha3KeccackHashProvider();

        private sealed class NullBytecodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class AccountRangeScheduler : IFetchRequestScheduler
        {
            private readonly PatriciaSnapRequestHandler _handler;
            private readonly bool _fail;
            public int AccountRangeCalls { get; private set; }

            public AccountRangeScheduler(PatriciaSnapRequestHandler handler, bool fail = false)
            {
                _handler = handler;
                _fail = fail;
            }

            public async Task<AccountRangeMessage> FetchAccountRangeAsync(
                byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
            {
                AccountRangeCalls++;
                if (_fail) throw new FetchRequestFailedException("account proof unavailable", null);
                return await _handler.GetAccountRangeAsync(new GetAccountRangeMessage
                {
                    RequestId = 1,
                    RootHash = stateRoot,
                    StartingHash = startingHash,
                    LimitHash = limitHash,
                    ResponseBytes = responseBytes,
                }, ct).ConfigureAwait(false);
            }

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        [Fact]
        public async Task Given_DeferredDebtAndFinalAccountRootChanged_When_Resolved_Then_FinalStorageRootIsUsedForSeeds()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var accountHash = Keccak.CalculateHash(new byte[] { 0x01, 0xAA });
            var discoveredStorageRoot = Hash32(0x11);
            var (server, finalStateRoot, finalStorageRoot) = BuildFinalState(accountHash, storageSeed: 0x22, includeAccount: true, emptyStorage: false);
            bundle.Metadata.UpsertDeferredStorageDebt(Debt(accountHash, discoveredStorageRoot));
            var scheduler = new AccountRangeScheduler(new PatriciaSnapRequestHandler(server, new NullBytecodes()));

            await DeferredStorageDebtFinalRootResolver.ResolveAsync(finalStateRoot, 100UL, bundle.Metadata, scheduler);

            var resolved = Assert.Single(bundle.Metadata.ListOpenDeferredStorageDebts());
            Assert.Equal(StorageCompleteness.FinalRootReResolved, resolved.Status);
            Assert.Equal(finalStateRoot, resolved.FinalStateRoot);
            Assert.Equal(finalStorageRoot, resolved.FinalStorageRoot);
            Assert.Equal(discoveredStorageRoot, resolved.DiscoveredStorageRoot);
            var seed = Assert.Single(DeferredStorageDebtFinalRootResolver.BuildFinalStorageSeeds(bundle.Metadata));
            Assert.Equal(accountHash, seed.AccountHash);
            Assert.Equal(finalStorageRoot, seed.StorageRoot);
        }

        [Fact]
        public async Task Given_FinalRootSeedCompleted_When_MarkedDeepComplete_Then_DebtIsNoLongerOpen()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var accountHash = Keccak.CalculateHash(new byte[] { 0x05, 0xEE });
            var discoveredStorageRoot = Hash32(0x12);
            var finalStorageRoot = Hash32(0x34);
            var debt = Debt(accountHash, discoveredStorageRoot);
            debt.Status = StorageCompleteness.FinalRootReResolved;
            debt.FinalStateRoot = Hash32(0x56);
            debt.FinalStorageRoot = finalStorageRoot;
            bundle.Metadata.UpsertDeferredStorageDebt(debt);

            DeferredStorageDebtFinalRootResolver.MarkFinalStorageSeedsDeepComplete(
                bundle.Metadata,
                new[] { (accountHash, finalStorageRoot) });

            Assert.Equal(0UL, bundle.Metadata.CountOpenDeferredStorageDebts());
            Assert.Empty(bundle.Metadata.ListOpenDeferredStorageDebts());
            Assert.Empty(DeferredStorageDebtFinalRootResolver.BuildFinalStorageSeeds(bundle.Metadata));
        }
        [Fact]
        public async Task Given_DeferredDebtAndAccountAbsentAtFinalRoot_When_Resolved_Then_DebtIsProofDropped()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var accountHash = Keccak.CalculateHash(new byte[] { 0x02, 0xBB });
            var discoveredStorageRoot = Hash32(0x33);
            var (server, finalStateRoot, _) = BuildFinalState(accountHash, storageSeed: 0x44, includeAccount: false, emptyStorage: false);
            bundle.Metadata.UpsertDeferredStorageDebt(Debt(accountHash, discoveredStorageRoot));
            var scheduler = new AccountRangeScheduler(new PatriciaSnapRequestHandler(server, new NullBytecodes()));

            await DeferredStorageDebtFinalRootResolver.ResolveAsync(finalStateRoot, 101UL, bundle.Metadata, scheduler);

            Assert.Equal(0UL, bundle.Metadata.CountOpenDeferredStorageDebts());
            Assert.Empty(DeferredStorageDebtFinalRootResolver.BuildFinalStorageSeeds(bundle.Metadata));
            var allRows = bundle.Metadata.ListOpenDeferredStorageDebts();
            Assert.Empty(allRows);
        }

        [Fact]
        public async Task Given_DeferredDebtAndAccountEmptyAtFinalRoot_When_Resolved_Then_DebtIsProofDropped()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var accountHash = Keccak.CalculateHash(new byte[] { 0x04, 0xDD });
            var discoveredStorageRoot = Hash32(0x77);
            var (server, finalStateRoot, _) = BuildFinalState(accountHash, storageSeed: 0x88, includeAccount: true, emptyStorage: true);
            bundle.Metadata.UpsertDeferredStorageDebt(Debt(accountHash, discoveredStorageRoot));
            var scheduler = new AccountRangeScheduler(new PatriciaSnapRequestHandler(server, new NullBytecodes()));

            await DeferredStorageDebtFinalRootResolver.ResolveAsync(finalStateRoot, 103UL, bundle.Metadata, scheduler);

            Assert.Equal(0UL, bundle.Metadata.CountOpenDeferredStorageDebts());
            Assert.Empty(DeferredStorageDebtFinalRootResolver.BuildFinalStorageSeeds(bundle.Metadata));
        }

        [Fact]
        public async Task Given_DeferredDebtAndAccountProofUnavailable_When_Resolved_Then_DebtStaysOpenOnDiscoveredRoot()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var accountHash = Keccak.CalculateHash(new byte[] { 0x03, 0xCC });
            var discoveredStorageRoot = Hash32(0x55);
            var (server, finalStateRoot, _) = BuildFinalState(accountHash, storageSeed: 0x66, includeAccount: true, emptyStorage: false);
            bundle.Metadata.UpsertDeferredStorageDebt(Debt(accountHash, discoveredStorageRoot));
            var scheduler = new AccountRangeScheduler(new PatriciaSnapRequestHandler(server, new NullBytecodes()), fail: true);

            await DeferredStorageDebtFinalRootResolver.ResolveAsync(finalStateRoot, 102UL, bundle.Metadata, scheduler);

            var unresolved = Assert.Single(bundle.Metadata.ListOpenDeferredStorageDebts());
            Assert.Equal(StorageCompleteness.DeferredBigAccount, unresolved.Status);
            Assert.Equal(discoveredStorageRoot, unresolved.DiscoveredStorageRoot);
            Assert.Null(unresolved.FinalStorageRoot);
            Assert.Empty(DeferredStorageDebtFinalRootResolver.BuildFinalStorageSeeds(bundle.Metadata));
        }

        private static DeferredStorageDebt Debt(byte[] accountHash, byte[] discoveredStorageRoot)
            => new DeferredStorageDebt
            {
                AccountHash = accountHash,
                DiscoveredStorageRoot = discoveredStorageRoot,
                FetchStateRoot = Hash32(0x99),
                FetchPivotBlock = 50UL,
                Reason = DeferredStorageReason.BigAccountSubrangeFailed,
                Status = StorageCompleteness.DeferredBigAccount,
            };

        private static (InMemoryContentNodeStore Server, byte[] StateRoot, byte[] StorageRoot) BuildFinalState(
            byte[] accountHash,
            byte storageSeed,
            bool includeAccount,
            bool emptyStorage)
        {
            var server = new InMemoryContentNodeStore();
            var storageRoot = emptyStorage ? DefaultValues.EMPTY_TRIE_HASH : BuildStorageRoot(server, storageSeed);
            var accountTrie = new PatriciaTrie(server, HashProvider);
            if (includeAccount)
            {
                var account = new AccountEncoder().Encode(new Account
                {
                    Nonce = (EvmUInt256)1,
                    Balance = (EvmUInt256)1000UL,
                    StateRoot = storageRoot,
                    CodeHash = DefaultValues.EMPTY_DATA_HASH,
                });
                accountTrie.Put(accountHash, account);
            }
            accountTrie.SaveDirtyNodesToStorage();
            return (server, accountTrie.Root.GetHash(), storageRoot);
        }

        private static byte[] BuildStorageRoot(InMemoryContentNodeStore server, byte seed)
        {
            var storageTrie = new PatriciaTrie(server, HashProvider);
            for (byte i = 1; i <= 2; i++)
            {
                var slotHash = Keccak.CalculateHash(new byte[] { seed, i });
                storageTrie.Put(slotHash, Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(seed + i) }));
            }
            storageTrie.SaveDirtyNodesToStorage();
            return storageTrie.Root.GetHash();
        }

        private static byte[] Hash32(byte value)
        {
            var hash = new byte[32];
            hash[31] = value;
            return hash;
        }
    }
}
