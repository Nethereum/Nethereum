using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Scheduling;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class FetchRequestSchedulerRangeProofQuarantineTests
    {
        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                await Task.Delay(50);
            }
            return condition();
        }

        private static byte[] FilledHash(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }

        private sealed class InMemoryBytecodeStore : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class DirectHandshakeWorker : IPeerHandshakeWorker
        {
            private readonly byte[] _genesisHash;
            private readonly ulong _networkId;
            public DirectHandshakeWorker(byte[] genesisHash, ulong networkId)
            {
                _genesisHash = genesisHash;
                _networkId = networkId;
            }
            public async Task<IEthPeer> HandshakeAsync(string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
                => await SyncPeerSession.ConnectAsync(enode, timeout, ct, _genesisHash, _networkId, minPeerLatestBlock);
        }

        private sealed class CountingSnapHandler : ISnapRequestHandler
        {
            private readonly ISnapRequestHandler _inner;
            public int AccountCalls;
            public int StorageCalls;
            public CountingSnapHandler(ISnapRequestHandler inner) => _inner = inner;
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
            {
                Interlocked.Increment(ref AccountCalls);
                return _inner.GetAccountRangeAsync(request, ct);
            }
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
            {
                Interlocked.Increment(ref StorageCalls);
                return _inner.GetStorageRangesAsync(request, ct);
            }
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(request, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(request, ct);
            public Task<Nethereum.Model.P2P.Snap.BlockAccessListsMessage> GetBlockAccessListsAsync(Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage request, CancellationToken ct = default)
                => _inner.GetBlockAccessListsAsync(request, ct);
        }

        private sealed class ProofInvalidAccountRangeSnapHandler : ISnapRequestHandler
        {
            private readonly byte[] _slimBody;
            public int AccountCalls;
            public ProofInvalidAccountRangeSnapHandler(byte[] slimBody) => _slimBody = slimBody;
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
            {
                Interlocked.Increment(ref AccountCalls);
                return Task.FromResult(new AccountRangeMessage
                {
                    RequestId = request.RequestId,
                    Accounts = new List<AccountRangeMessage.AccountEntry>
                    {
                        new AccountRangeMessage.AccountEntry { Hash = FilledHash(0x11), Body = _slimBody },
                    },
                    Proof = new List<byte[]>(),
                });
            }
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<Nethereum.Model.P2P.Snap.BlockAccessListsMessage> GetBlockAccessListsAsync(Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
        }

        private sealed class ProofInvalidStorageRangeSnapHandler : ISnapRequestHandler
        {
            public int StorageCalls;
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
            {
                Interlocked.Increment(ref StorageCalls);
                return Task.FromResult(new StorageRangesMessage
                {
                    RequestId = request.RequestId,
                    Slots = new List<List<StorageRangesMessage.SlotEntry>>
                    {
                        new List<StorageRangesMessage.SlotEntry>
                        {
                            new StorageRangesMessage.SlotEntry { Hash = FilledHash(0x22), Data = new byte[] { 0xAB } },
                        },
                    },
                    Proof = new List<byte[]>(),
                });
            }
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<Nethereum.Model.P2P.Snap.BlockAccessListsMessage> GetBlockAccessListsAsync(Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
        }

        private sealed class EmptyWithBogusProofAccountRangeSnapHandler : ISnapRequestHandler
        {
            public int AccountCalls;
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
            {
                Interlocked.Increment(ref AccountCalls);
                return Task.FromResult(new AccountRangeMessage
                {
                    RequestId = request.RequestId,
                    Accounts = new List<AccountRangeMessage.AccountEntry>(),
                    Proof = new List<byte[]> { new byte[] { 0x01, 0x02, 0x03 } },
                });
            }
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<Nethereum.Model.P2P.Snap.BlockAccessListsMessage> GetBlockAccessListsAsync(Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
        }

        private static readonly byte[] GenesisHash = new byte[32];
        private const ulong NetworkId = 1;

        private static async Task<(PeerListener Listener, string Enode)> StartListenerAsync(ISnapRequestHandler handler)
        {
            var bundle = InMemoryChainStoreBundle.Open();
            await bundle.Blocks.SaveAsync(new Nethereum.Model.BlockHeader { BlockNumber = 0 }, new byte[32]);
            var options = new PeerListenerOptions
            {
                ListenPort = 0,
                BindAddress = IPAddress.Loopback,
                MaxInboundPeers = 5,
                MaxInboundPerIP = 5,
                ServeSnap = true,
                MirrorRemoteStatus = false,
            };
            var status = new Eth68StatusMessage
            {
                ProtocolVersion = 68,
                NetworkId = NetworkId,
                TotalDifficulty = BigInteger.One,
                BestHash = GenesisHash,
                GenesisHash = GenesisHash,
                ForkHash = 0,
                ForkNext = 0,
            };
            var key = EthECKey.GenerateKey();
            var listener = new PeerListener(key, bundle, options, status, snapHandler: handler);
            await listener.StartAsync();
            var enode = $"enode://{key.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{listener.Port}";
            return (listener, enode);
        }

        private static (byte[] StateRoot, byte[] StorageRoot, byte[] Owner, List<(byte[] Key, byte[] Value)> Entries) BuildOwnerState(
            InMemoryContentNodeStore store, int count, int seed)
        {
            var owner = new byte[32];
            owner[0] = 0x40;
            owner[31] = 0x02;

            var keccak = new Sha3Keccack();
            var storageTrie = new PatriciaTrie(store, owner);
            var entries = new List<(byte[] Key, byte[] Value)>();
            for (int i = 0; i < count; i++)
            {
                var key = keccak.CalculateHash(new[] { (byte)((seed >> 8) & 0xff), (byte)(seed & 0xff), (byte)i });
                var value = new byte[] { (byte)(i & 0xff), 0xEF };
                entries.Add((key, value));
                storageTrie.Put(key, value);
            }
            storageTrie.SaveDirtyNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            var account = new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            };
            var stateTrie = new PatriciaTrie(store);
            stateTrie.Put(owner, new AccountEncoder().Encode(account));
            stateTrie.SaveDirtyNodesToStorage();

            return (stateTrie.Root.GetHash(), storageRoot, owner, entries);
        }

        private static bool VerifyAccountRange(byte[] stateRoot, AccountRangeMessage r)
        {
            var proof = (IList<byte[]>)(r?.Proof ?? new List<byte[]>());
            if (r?.Accounts == null || r.Accounts.Count == 0)
                return ProofVerification.Current.Range.Verify(
                    stateRoot, new byte[32], Array.Empty<byte[]>(), Array.Empty<byte[]>(), proof).Valid;
            var keys = new List<byte[]>(r.Accounts.Count);
            var values = new List<byte[]>(r.Accounts.Count);
            foreach (var e in r.Accounts)
            {
                keys.Add(e.Hash);
                values.Add(SlimAccountEncoder.FromSlim(e.Body));
            }
            return ProofVerification.Current.Range.Verify(stateRoot, new byte[32], keys, values, proof).Valid;
        }

        private static bool VerifyStorageRange(byte[] storageRoot, StorageRangesMessage r)
        {
            if (r?.Slots == null || r.Slots.Count == 0 || r.Slots[0].Count == 0) return true;
            var slots = r.Slots[0];
            var keys = new List<byte[]>(slots.Count);
            var values = new List<byte[]>(slots.Count);
            foreach (var s in slots)
            {
                keys.Add(s.Hash);
                values.Add(s.Data);
            }
            var proof = (IList<byte[]>)(r.Proof ?? new List<byte[]>());
            return ProofVerification.Current.Range.Verify(storageRoot, new byte[32], keys, values, proof).Valid;
        }

        [Fact]
        public async Task AccountRange_ProofInvalidResponse_PeerQuarantinedAndRotatesToServingPeer()
        {
            var store = new InMemoryContentNodeStore();
            var (stateRoot, _, owner, _) = BuildOwnerState(store, count: 4, seed: 7);

            var slimBody = SlimAccountEncoder.ToSlim(new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));

            var goodHandler = new CountingSnapHandler(new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore()));
            var badHandler = new ProofInvalidAccountRangeSnapHandler(slimBody);

            var (badListener, badEnode) = await StartListenerAsync(badHandler);
            var (goodListener, goodEnode) = await StartListenerAsync(goodHandler);
            await using var _badListenerDisposal = badListener;
            await using var _goodListenerDisposal = goodListener;

            var pool = new PeerPoolManager(
                new DirectHandshakeWorker(GenesisHash, NetworkId),
                new PeerPoolOptions(TargetPeerCount: 2, MinPeerLatestBlock: 0));
            await pool.StartAsync(CancellationToken.None);
            await using var _poolDisposal = pool;

            pool.EnqueueCandidate(badEnode);
            Assert.True(
                await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap), TimeSpan.FromSeconds(20)),
                "bad peer never became snap-capable");
            var badPeerId = pool.ActivePeers.OfType<SyncPeerSession>().Single().Id;

            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());

            using (var primeCts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                try
                {
                    await scheduler.FetchAccountRangeAsync(
                        stateRoot, new byte[32], FilledHash(0xff), 524_288UL,
                        r => VerifyAccountRange(stateRoot, r), primeCts.Token);
                }
                catch (OperationCanceledException) { }
                catch (FetchRequestFailedException) { }
            }

            Assert.True(scheduler.IsSnapStateQuarantined(badPeerId), "the proof-invalid peer must be quarantined");

            pool.EnqueueCandidate(goodEnode);
            Assert.True(
                await WaitUntilAsync(
                    () => pool.ActivePeers.OfType<SyncPeerSession>().Count(p => p.SupportsSnap) >= 2,
                    TimeSpan.FromSeconds(20)),
                "good peer never became snap-capable");

            using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await scheduler.FetchAccountRangeAsync(
                stateRoot, new byte[32], FilledHash(0xff), 524_288UL,
                r => VerifyAccountRange(stateRoot, r), testCts.Token);

            Assert.True(goodHandler.AccountCalls >= 1, "the serving peer must actually have been reached");
            Assert.Single(result.Accounts);
            Assert.Equal(owner, result.Accounts[0].Hash);
            Assert.True(VerifyAccountRange(stateRoot, result), "the returned account range must verify against the state root");
            Assert.True(scheduler.IsSnapStateQuarantined(badPeerId), "the proof-invalid peer must remain quarantined");
        }

        [Fact]
        public async Task AccountRange_EmptyAccountsWithBogusAbsenceProof_PeerQuarantinedAndRotatesToServingPeer()
        {
            var store = new InMemoryContentNodeStore();
            var (stateRoot, _, owner, _) = BuildOwnerState(store, count: 4, seed: 13);

            var goodHandler = new CountingSnapHandler(new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore()));
            var badHandler = new EmptyWithBogusProofAccountRangeSnapHandler();

            var (badListener, badEnode) = await StartListenerAsync(badHandler);
            var (goodListener, goodEnode) = await StartListenerAsync(goodHandler);
            await using var _badListenerDisposal = badListener;
            await using var _goodListenerDisposal = goodListener;

            var pool = new PeerPoolManager(
                new DirectHandshakeWorker(GenesisHash, NetworkId),
                new PeerPoolOptions(TargetPeerCount: 2, MinPeerLatestBlock: 0));
            await pool.StartAsync(CancellationToken.None);
            await using var _poolDisposal = pool;

            pool.EnqueueCandidate(badEnode);
            Assert.True(
                await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap), TimeSpan.FromSeconds(20)),
                "bad peer never became snap-capable");
            var badPeerId = pool.ActivePeers.OfType<SyncPeerSession>().Single().Id;

            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());

            using (var primeCts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                try
                {
                    await scheduler.FetchAccountRangeAsync(
                        stateRoot, new byte[32], FilledHash(0xff), 524_288UL,
                        r => VerifyAccountRange(stateRoot, r), primeCts.Token);
                }
                catch (OperationCanceledException) { }
                catch (FetchRequestFailedException) { }
            }

            Assert.True(scheduler.IsSnapStateQuarantined(badPeerId), "the empty+bogus-proof peer must be quarantined");

            pool.EnqueueCandidate(goodEnode);
            Assert.True(
                await WaitUntilAsync(
                    () => pool.ActivePeers.OfType<SyncPeerSession>().Count(p => p.SupportsSnap) >= 2,
                    TimeSpan.FromSeconds(20)),
                "good peer never became snap-capable");

            using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await scheduler.FetchAccountRangeAsync(
                stateRoot, new byte[32], FilledHash(0xff), 524_288UL,
                r => VerifyAccountRange(stateRoot, r), testCts.Token);

            Assert.True(goodHandler.AccountCalls >= 1, "the serving peer must actually have been reached");
            Assert.Single(result.Accounts);
            Assert.Equal(owner, result.Accounts[0].Hash);
            Assert.True(scheduler.IsSnapStateQuarantined(badPeerId), "the empty+bogus-proof peer must remain quarantined");
        }

        [Fact]
        public async Task StorageRange_ProofInvalidResponse_PeerQuarantinedAndRotatesToServingPeer()
        {
            var store = new InMemoryContentNodeStore();
            var (stateRoot, storageRoot, owner, entries) = BuildOwnerState(store, count: 4, seed: 11);

            var goodHandler = new CountingSnapHandler(new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore()));
            var badHandler = new ProofInvalidStorageRangeSnapHandler();

            var (badListener, badEnode) = await StartListenerAsync(badHandler);
            var (goodListener, goodEnode) = await StartListenerAsync(goodHandler);
            await using var _badListenerDisposal = badListener;
            await using var _goodListenerDisposal = goodListener;

            var pool = new PeerPoolManager(
                new DirectHandshakeWorker(GenesisHash, NetworkId),
                new PeerPoolOptions(TargetPeerCount: 2, MinPeerLatestBlock: 0));
            await pool.StartAsync(CancellationToken.None);
            await using var _poolDisposal = pool;

            pool.EnqueueCandidate(badEnode);
            Assert.True(
                await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap), TimeSpan.FromSeconds(20)),
                "bad peer never became snap-capable");
            var badPeerId = pool.ActivePeers.OfType<SyncPeerSession>().Single().Id;

            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());

            using (var primeCts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                try
                {
                    await scheduler.FetchStorageRangesAsync(
                        stateRoot, new List<byte[]> { owner }, new byte[32], FilledHash(0xff), 524_288UL,
                        r => VerifyStorageRange(storageRoot, r), primeCts.Token);
                }
                catch (OperationCanceledException) { }
                catch (FetchRequestFailedException) { }
            }

            Assert.True(scheduler.IsSnapStateQuarantined(badPeerId), "the proof-invalid peer must be quarantined");

            pool.EnqueueCandidate(goodEnode);
            Assert.True(
                await WaitUntilAsync(
                    () => pool.ActivePeers.OfType<SyncPeerSession>().Count(p => p.SupportsSnap) >= 2,
                    TimeSpan.FromSeconds(20)),
                "good peer never became snap-capable");

            using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await scheduler.FetchStorageRangesAsync(
                stateRoot, new List<byte[]> { owner }, new byte[32], FilledHash(0xff), 524_288UL,
                r => VerifyStorageRange(storageRoot, r), testCts.Token);

            Assert.True(goodHandler.StorageCalls >= 1, "the serving peer must actually have been reached");
            Assert.NotEmpty(result.Slots);
            Assert.NotEmpty(result.Slots[0]);
            Assert.True(VerifyStorageRange(storageRoot, result), "the returned storage range must verify against the storage root");

            var returned = result.Slots[0].ToDictionary(s => s.Hash.ToHex(), s => s.Data);
            foreach (var (key, value) in entries)
                Assert.Equal(value, returned[key.ToHex()]);

            Assert.True(scheduler.IsSnapStateQuarantined(badPeerId), "the proof-invalid peer must remain quarantined");
        }

        private static void PromoteWhale(SnapTaskSet set, byte[] owner, byte[] storageRoot, byte[] stateRootAtCreation)
        {
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            set.CompleteAccountRange(frag,
                new List<AccountClassification> { new(owner, storageRoot, DefaultValues.EMPTY_DATA_HASH) },
                lastHash: owner, done: true);
            var batch = (SnapFragment.SmallStorageBatch)set.LeaseNext();
            set.CompleteSmallBatch(batch,
                new List<SmallStorageOutcome> { new(owner, SmallStorageResult.Large, new byte[32]) }, stateRootAtCreation);
        }

        private sealed class EmptyWithBogusProofStorageRangeSnapHandler : ISnapRequestHandler
        {
            public int StorageCalls;
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
            {
                Interlocked.Increment(ref StorageCalls);
                return Task.FromResult(new StorageRangesMessage
                {
                    RequestId = request.RequestId,
                    Slots = new List<List<StorageRangesMessage.SlotEntry>> { new List<StorageRangesMessage.SlotEntry>() },
                    Proof = new List<byte[]> { new byte[] { 0x0A, 0x0B, 0x0C } },
                });
            }
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<Nethereum.Model.P2P.Snap.BlockAccessListsMessage> GetBlockAccessListsAsync(Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
        }

        [Fact]
        public Task StorageRange_SnapSyncClientSubtask_NonEmptyProofInvalidPeerQuarantined_ServingPeerDrains()
            => RunSnapSyncClientSubtaskQuarantineDrainAsync(new ProofInvalidStorageRangeSnapHandler());

        [Fact]
        public Task StorageRange_SnapSyncClientSubtask_EmptyWithBogusProofPeerQuarantined_ServingPeerDrains()
            => RunSnapSyncClientSubtaskQuarantineDrainAsync(new EmptyWithBogusProofStorageRangeSnapHandler());

        private async Task RunSnapSyncClientSubtaskQuarantineDrainAsync(ISnapRequestHandler badHandler)
        {
            var store = new InMemoryContentNodeStore();
            var (stateRoot, storageRoot, owner, entries) = BuildOwnerState(store, count: 30, seed: 17);

            var goodHandler = new CountingSnapHandler(new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore()));

            var (badListener, badEnode) = await StartListenerAsync(badHandler);
            var (goodListener, goodEnode) = await StartListenerAsync(goodHandler);
            await using var _badListenerDisposal = badListener;
            await using var _goodListenerDisposal = goodListener;

            var pool = new PeerPoolManager(
                new DirectHandshakeWorker(GenesisHash, NetworkId),
                new PeerPoolOptions(TargetPeerCount: 2, MinPeerLatestBlock: 0));
            await pool.StartAsync(CancellationToken.None);
            await using var _poolDisposal = pool;

            pool.EnqueueCandidate(badEnode);
            Assert.True(
                await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap), TimeSpan.FromSeconds(20)),
                "bad peer never became snap-capable");
            var badPeerId = pool.ActivePeers.OfType<SyncPeerSession>().Single().Id;

            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());
            var client = new SnapSyncClient(new SchedulerSnapPeer(scheduler), responseBytesBudget: 300UL);

            var set = new SnapTaskSet(1) { LargeContractConcurrency = 1 };
            PromoteWhale(set, owner, storageRoot, stateRoot);
            var clientStore = new InMemoryContentNodeStore();
            var accountsNeedingHeal = new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>();
            var deferredStorageDebts = new ConcurrentDictionary<string, DeferredStorageDebt>();

            var primeLease = (SnapFragment.StorageSubtask)set.LeaseNext();
            using (var primeCts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                try
                {
                    await client.ProcessStorageSubtaskAsync(
                        primeLease, set, clientStore, stateRoot, accountsNeedingHeal, deferredStorageDebts, null, primeCts.Token);
                }
                catch (OperationCanceledException) { }
                catch (FetchRequestFailedException) { }
            }
            set.Revert(primeLease);
            Assert.True(scheduler.IsSnapStateQuarantined(badPeerId),
                "the proof-invalid storage peer must be quarantined by the SnapSyncClient fetch delegate");

            pool.EnqueueCandidate(goodEnode);
            Assert.True(
                await WaitUntilAsync(
                    () => pool.ActivePeers.OfType<SyncPeerSession>().Count(p => p.SupportsSnap) >= 2,
                    TimeSpan.FromSeconds(20)),
                "good peer never became snap-capable");

            using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var rounds = 0;
            object observedScope = null;
            SnapFragment.StorageSubtask lease;
            while ((lease = set.LeaseNext() as SnapFragment.StorageSubtask) != null)
            {
                Assert.True(++rounds <= 2000,
                    "subtask never drained -- a byzantine page must be quarantined at fetch, not spun on forever");
                await client.ProcessStorageSubtaskAsync(
                    lease, set, clientStore, stateRoot, accountsNeedingHeal, deferredStorageDebts, null, testCts.Token);
                var scopeAfter = set.Tasks[0].LargeContracts[owner].Scope;
                if (scopeAfter != null) observedScope = scopeAfter;
            }

            Assert.True(goodHandler.StorageCalls >= 1, "the serving peer must actually have been reached");
            Assert.True(set.AllDone);
            Assert.Contains(owner, set.Tasks[0].StorageCompleted, ByteArrayComparer.Current);
            Assert.DoesNotContain(accountsNeedingHeal, a => ByteArrayComparer.Current.Equals(a.AccountHash, owner));
            Assert.DoesNotContain(deferredStorageDebts.Values, d => ByteArrayComparer.Current.Equals(d.AccountHash, owner));
            Assert.True(scheduler.IsSnapStateQuarantined(badPeerId), "the proof-invalid peer must remain quarantined");

            Assert.NotNull(observedScope);
            var drainedScope = (ResumableStorageScope)observedScope;
            foreach (var (key, value) in entries)
                Assert.Equal(value, drainedScope.Get(key));
        }

        [Fact]
        public void VerifyBatchStorageResponse_OverLengthAndEmptyBogusProof_Rejected_ValidCompleteSetPasses()
        {
            var store = new InMemoryContentNodeStore();
            var (_, storageRoot, owner, entries) = BuildOwnerState(store, count: 4, seed: 23);
            var dispatched = new List<(byte[] Hash, byte[] Root)> { (owner, storageRoot) };

            var overLength = new StorageRangesMessage
            {
                Slots = new List<List<StorageRangesMessage.SlotEntry>>
                {
                    new List<StorageRangesMessage.SlotEntry> { new() { Hash = FilledHash(0x01), Data = new byte[] { 0x01 } } },
                    new List<StorageRangesMessage.SlotEntry> { new() { Hash = FilledHash(0x02), Data = new byte[] { 0x02 } } },
                },
                Proof = new List<byte[]>(),
            };
            Assert.False(SnapProofVerifier.VerifyBatchStorageResponse(dispatched, overLength),
                "an over-length batch response must be quarantined, not passed through to a downstream crash");

            var emptyBogus = new StorageRangesMessage
            {
                Slots = new List<List<StorageRangesMessage.SlotEntry>> { new List<StorageRangesMessage.SlotEntry>() },
                Proof = new List<byte[]> { new byte[] { 0x0A, 0x0B, 0x0C } },
            };
            Assert.False(SnapProofVerifier.VerifyBatchStorageResponse(dispatched, emptyBogus),
                "an empty inner set + bogus proof for a non-empty storage root must be quarantined");

            var complete = new StorageRangesMessage
            {
                Slots = new List<List<StorageRangesMessage.SlotEntry>>
                {
                    entries.OrderBy(e => e.Key, ByteArrayComparer.Current)
                           .Select(e => new StorageRangesMessage.SlotEntry { Hash = e.Key, Data = e.Value })
                           .ToList(),
                },
                Proof = new List<byte[]>(),
            };
            Assert.True(SnapProofVerifier.VerifyBatchStorageResponse(dispatched, complete),
                "an honest complete set that reconstructs its storage root must pass");
        }
    }
}
