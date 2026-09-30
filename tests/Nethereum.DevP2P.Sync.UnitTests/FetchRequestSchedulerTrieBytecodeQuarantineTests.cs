using System;
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
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Peering;
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
    public class FetchRequestSchedulerTrieBytecodeQuarantineTests
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

        private sealed class DictionaryBytecodeStore : IBytecodeStore
        {
            private readonly Dictionary<string, byte[]> _codes = new();
            public void Put(byte[] codeHash, byte[] code) => _codes[codeHash.ToHex()] = code;
            public byte[] Get(byte[] codeHash) => _codes.TryGetValue(codeHash.ToHex(), out var c) ? c : null;
        }

        private sealed class CountingSnapHandler : ISnapRequestHandler
        {
            private readonly ISnapRequestHandler _inner;
            public int TrieCalls;
            public int CodeCalls;
            public CountingSnapHandler(ISnapRequestHandler inner) => _inner = inner;
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
                => _inner.GetAccountRangeAsync(request, ct);
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
                => _inner.GetStorageRangesAsync(request, ct);
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
            {
                Interlocked.Increment(ref CodeCalls);
                return _inner.GetByteCodesAsync(request, ct);
            }
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
            {
                Interlocked.Increment(ref TrieCalls);
                return _inner.GetTrieNodesAsync(request, ct);
            }
            public Task<Nethereum.Model.P2P.Snap.BlockAccessListsMessage> GetBlockAccessListsAsync(Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage request, CancellationToken ct = default)
                => _inner.GetBlockAccessListsAsync(request, ct);
        }

        private sealed class HashMismatchTrieNodeSnapHandler : ISnapRequestHandler
        {
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
                => Task.FromResult(new TrieNodesMessage
                {
                    RequestId = request.RequestId,
                    Nodes = new List<byte[]> { new byte[] { 0xDE, 0xAD, 0xBE, 0xEF } },
                });
            public Task<Nethereum.Model.P2P.Snap.BlockAccessListsMessage> GetBlockAccessListsAsync(Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
        }

        private sealed class HashMismatchByteCodeSnapHandler : ISnapRequestHandler
        {
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
                => Task.FromResult(new ByteCodesMessage
                {
                    RequestId = request.RequestId,
                    Codes = new List<byte[]> { new byte[] { 0x01, 0x02, 0x03, 0x04 } },
                });
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
            public Task<Nethereum.Model.P2P.Snap.BlockAccessListsMessage> GetBlockAccessListsAsync(Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage request, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");
        }

        private sealed class DecodeFaultingAccountRangeSnapHandler : ISnapRequestHandler
        {
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
                => Task.FromResult(new AccountRangeMessage
                {
                    RequestId = request.RequestId,
                    Accounts = new List<AccountRangeMessage.AccountEntry>
                    {
                        new AccountRangeMessage.AccountEntry { Hash = FilledHash(0x11), Body = new byte[] { 0x00 } },
                    },
                    Proof = new List<byte[]>(),
                });
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

        private static async Task<(PeerListener Listener, string Enode)> StartListenerAsync(ISnapRequestHandler handler)
        {
            var bundle = InMemoryChainStoreBundle.Open();
            await bundle.Blocks.SaveAsync(new BlockHeader { BlockNumber = 0 }, new byte[32]);
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

        private static (byte[] StateRoot, byte[] Owner) BuildAccountState(InMemoryContentNodeStore store, int seed)
        {
            var owner = new byte[32];
            owner[0] = 0x40;
            owner[31] = (byte)seed;
            var account = new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            };
            var stateTrie = new PatriciaTrie(store);
            stateTrie.Put(owner, new AccountEncoder().Encode(account));
            stateTrie.SaveDirtyNodesToStorage();
            return (stateTrie.Root.GetHash(), owner);
        }

        private static bool VerifyTrieNodes(IReadOnlyList<byte[]> expectedHashes, TrieNodesMessage resp)
        {
            if (resp?.Nodes == null || resp.Nodes.Count == 0) return true;
            var keccak = new Nethereum.Util.HashProviders.Sha3KeccackHashProvider();
            var wanted = new Dictionary<byte[], int>(ByteArrayComparer.Current);
            foreach (var h in expectedHashes)
                wanted[h] = wanted.TryGetValue(h, out var c) ? c + 1 : 1;
            foreach (var blob in resp.Nodes)
            {
                if (blob == null || blob.Length == 0) continue;
                var hash = keccak.ComputeHash(blob);
                if (wanted.TryGetValue(hash, out var remaining) && remaining > 0)
                    wanted[hash] = remaining - 1;
                else
                    return false;
            }
            return true;
        }

        [Fact]
        public async Task TrieNodes_HashMismatchedNode_PeerQuarantinedAndRotatesToServingPeer()
        {
            var store = new InMemoryContentNodeStore();
            var (stateRoot, _) = BuildAccountState(store, seed: 3);
            var rootPath = new List<List<byte[]>> { new List<byte[]> { PatriciaPathWalker.NibblesToCompact(Array.Empty<byte>()) } };
            var expected = new List<byte[]> { stateRoot };

            var goodHandler = new CountingSnapHandler(new PatriciaSnapRequestHandler(store, new DictionaryBytecodeStore()));
            var badHandler = new HashMismatchTrieNodeSnapHandler();

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
                    await scheduler.FetchTrieNodesAsync(
                        stateRoot, rootPath, 524_288UL, r => VerifyTrieNodes(expected, r), primeCts.Token);
                }
                catch (OperationCanceledException) { }
                catch (FetchRequestFailedException) { }
            }

            Assert.True(scheduler.IsSnapStateQuarantined(badPeerId), "the hash-mismatched trie-node peer must be quarantined");

            pool.EnqueueCandidate(goodEnode);
            Assert.True(
                await WaitUntilAsync(
                    () => pool.ActivePeers.OfType<SyncPeerSession>().Count(p => p.SupportsSnap) >= 2,
                    TimeSpan.FromSeconds(20)),
                "good peer never became snap-capable");

            using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await scheduler.FetchTrieNodesAsync(
                stateRoot, rootPath, 524_288UL, r => VerifyTrieNodes(expected, r), testCts.Token);

            Assert.True(goodHandler.TrieCalls >= 1, "the serving peer must actually have been reached");
            Assert.Single(result.Nodes);
            Assert.True(VerifyTrieNodes(expected, result), "the returned node must hash to the requested state root");
            Assert.Equal(stateRoot, new Nethereum.Util.HashProviders.Sha3KeccackHashProvider().ComputeHash(result.Nodes[0]));
            Assert.True(scheduler.IsSnapStateQuarantined(badPeerId), "the hash-mismatched trie-node peer must remain quarantined");
        }

        [Fact]
        public async Task ByteCodes_HashMismatchedCode_PeerQuarantinedAndRotatesToServingPeer()
        {
            var code = new byte[] { 0x60, 0x00, 0x60, 0x00, 0xF3 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);
            var bytecodes = new DictionaryBytecodeStore();
            bytecodes.Put(codeHash, code);
            var requested = new List<byte[]> { codeHash };

            var store = new InMemoryContentNodeStore();
            var goodHandler = new CountingSnapHandler(new PatriciaSnapRequestHandler(store, bytecodes));
            var badHandler = new HashMismatchByteCodeSnapHandler();

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
                    await scheduler.FetchByteCodesAsync(
                        requested, 524_288UL, r => SnapProofVerifier.VerifyByteCodesResponse(requested, r), primeCts.Token);
                }
                catch (OperationCanceledException) { }
                catch (FetchRequestFailedException) { }
            }

            Assert.True(scheduler.IsSnapStateQuarantined(badPeerId), "the hash-mismatched bytecode peer must be quarantined");

            pool.EnqueueCandidate(goodEnode);
            Assert.True(
                await WaitUntilAsync(
                    () => pool.ActivePeers.OfType<SyncPeerSession>().Count(p => p.SupportsSnap) >= 2,
                    TimeSpan.FromSeconds(20)),
                "good peer never became snap-capable");

            using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await scheduler.FetchByteCodesAsync(
                requested, 524_288UL, r => SnapProofVerifier.VerifyByteCodesResponse(requested, r), testCts.Token);

            Assert.True(goodHandler.CodeCalls >= 1, "the serving peer must actually have been reached");
            Assert.Single(result.Codes);
            Assert.Equal(code, result.Codes[0]);
            Assert.Equal(codeHash, Sha3Keccack.Current.CalculateHash(result.Codes[0]));
            Assert.True(scheduler.IsSnapStateQuarantined(badPeerId), "the hash-mismatched bytecode peer must remain quarantined");
        }

        [Fact]
        public async Task AccountRange_DecodeFaultingResponse_PeerQuarantinedAndRotatesToServingPeer()
        {
            var store = new InMemoryContentNodeStore();
            var (stateRoot, owner) = BuildAccountState(store, seed: 9);

            var goodHandler = new CountingSnapHandler(new PatriciaSnapRequestHandler(store, new DictionaryBytecodeStore()));
            var badHandler = new DecodeFaultingAccountRangeSnapHandler();

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
                        stateRoot, new byte[32], FilledHash(0xff), 524_288UL, VerifyAccountRange, primeCts.Token);
                }
                catch (OperationCanceledException) { }
                catch (FetchRequestFailedException) { }
            }

            Assert.True(scheduler.IsSnapStateQuarantined(badPeerId), "the decode-faulting peer must be quarantined");

            pool.EnqueueCandidate(goodEnode);
            Assert.True(
                await WaitUntilAsync(
                    () => pool.ActivePeers.OfType<SyncPeerSession>().Count(p => p.SupportsSnap) >= 2,
                    TimeSpan.FromSeconds(20)),
                "good peer never became snap-capable");

            using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await scheduler.FetchAccountRangeAsync(
                stateRoot, new byte[32], FilledHash(0xff), 524_288UL, VerifyAccountRange, testCts.Token);

            Assert.Single(result.Accounts);
            Assert.Equal(owner, result.Accounts[0].Hash);
            Assert.True(VerifyAccountRange(result), "the returned account range must verify against the state root");
            Assert.True(scheduler.IsSnapStateQuarantined(badPeerId), "the decode-faulting peer must remain quarantined");

            bool VerifyAccountRange(AccountRangeMessage r)
            {
                var proof = (IList<byte[]>)(r?.Proof ?? new List<byte[]>());
                if (r?.Accounts == null || r.Accounts.Count == 0)
                    return Nethereum.Merkle.Patricia.ProofVerification.ProofVerification.Current.Range.Verify(
                        stateRoot, new byte[32], Array.Empty<byte[]>(), Array.Empty<byte[]>(), proof).Valid;
                var keys = new List<byte[]>(r.Accounts.Count);
                var values = new List<byte[]>(r.Accounts.Count);
                foreach (var e in r.Accounts)
                {
                    keys.Add(e.Hash);
                    values.Add(SlimAccountEncoder.FromSlim(e.Body));
                }
                return Nethereum.Merkle.Patricia.ProofVerification.ProofVerification.Current.Range.Verify(
                    stateRoot, new byte[32], keys, values, proof).Valid;
            }
        }
    }
}
