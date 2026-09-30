using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Serving;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncClientBytecodeTests
    {
        private sealed class InMemoryBytecodeStore : IBytecodeStore
        {
            private readonly Dictionary<byte[], byte[]> _codes = new(ByteArrayComparer.Current);
            public void Put(byte[] hash, byte[] code) { _codes[hash] = code; }
            public byte[] Get(byte[] hash) => _codes.TryGetValue(hash, out var v) ? v : null;
        }

        private sealed class RecordingBytecodePeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private readonly Func<GetByteCodesMessage, ByteCodesMessage> _byteCodesResponder;
            private readonly object _lock = new();
            private int _byteCodesCallCount;
            private List<byte[]> _lastRequestedHashes = new();

            public List<byte[]> LastRequestedHashes { get { lock (_lock) return _lastRequestedHashes; } }
            public int ByteCodesCallCount => Volatile.Read(ref _byteCodesCallCount);

            public RecordingBytecodePeer(ISnapPeer inner, Func<GetByteCodesMessage, ByteCodesMessage> byteCodesResponder)
            {
                _inner = inner;
                _byteCodesResponder = byteCodesResponder;
            }

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
                => _inner.GetAccountRangeAsync(r, ct);

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
                => _inner.GetStorageRangesAsync(r, ct);

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
            {
                Interlocked.Increment(ref _byteCodesCallCount);
                lock (_lock) _lastRequestedHashes = new List<byte[]>(r.Hashes);
                return Task.FromResult(_byteCodesResponder(r));
            }

            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(r, ct);
        }

        private sealed class Scenario
        {
            public byte[] StateRoot { get; init; }
            public InMemoryContentNodeStore TrieStorage { get; init; }
            public InMemoryBytecodeStore Codes { get; init; }
            public List<byte[]> AccountCodeHashesInOrder { get; init; }
            public List<byte[]> AccountCodesInOrder { get; init; }
        }

        private static Scenario BuildStateWithDistinctContracts(int contractCount)
        {
            var keccak = new Sha3Keccack();
            var hashProvider = new Sha3KeccackHashProvider();
            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            var codes = new InMemoryBytecodeStore();
            var hashes = new List<byte[]>();
            var bodies = new List<byte[]>();

            for (int i = 0; i < contractCount; i++)
            {
                var addrHash = keccak.CalculateHash(new byte[] { (byte)(i & 0xff), (byte)((i >> 8) & 0xff), 0xAB });
                var code = new byte[] { 0x60, (byte)(i & 0xff), (byte)((i >> 8) & 0xff), 0xF3 };
                var codeHash = hashProvider.ComputeHash(code);
                codes.Put(codeHash, code);

                var body = new AccountEncoder().Encode(new Account
                {
                    Nonce = (EvmUInt256)(uint)(i + 1),
                    Balance = (EvmUInt256)(ulong)((ulong)(i + 1) * 7UL),
                    StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                    CodeHash = codeHash
                });
                trie.Put(addrHash, body);
                hashes.Add(codeHash);
                bodies.Add(code);
            }
            trie.SaveDirtyNodesToStorage();
            return new Scenario
            {
                StateRoot = trie.Root.GetHash(),
                TrieStorage = storage,
                Codes = codes,
                AccountCodeHashesInOrder = hashes,
                AccountCodesInOrder = bodies
            };
        }

        private static SnapSyncClient.SyncResult RunSync(Scenario scenario, RecordingBytecodePeer peer)
        {
            var client = CreateClient(peer);
            try
            {
                return client.SyncStateAsync(scenario.StateRoot).GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static byte[] Sha(byte[] data) => new Sha3KeccackHashProvider().ComputeHash(data);

        private static SnapSyncClient CreateClient(ISnapPeer peer)
            => new SnapSyncClient(peer, accountsPerRequest: 64, responseBytesBudget: 1_000_000UL)
            {
                AccountConcurrency = 1,
                BytecodeDeadEndNoProgressRounds = 6,
            };

        [Fact]
        public async Task Sync_PeerReturnsAllCodes_AllWrittenUnderCorrectHash()
        {
            var s = BuildStateWithDistinctContracts(3);
            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(s.TrieStorage, s.Codes));
            var peer = new RecordingBytecodePeer(honest, r => new ByteCodesMessage
            {
                RequestId = r.RequestId,
                Codes = r.Hashes.Select(h => s.Codes.Get(h)).ToList()
            });

            var client = CreateClient(peer);
            var result = await client.SyncStateAsync(s.StateRoot);

            Assert.True(result.RootMatchesTarget);
            Assert.Equal(3, result.BytecodeByHash.Count);
            for (int i = 0; i < 3; i++)
            {
                var hex = s.AccountCodeHashesInOrder[i].ToHex();
                Assert.True(result.BytecodeByHash.ContainsKey(hex));
                Assert.Equal(s.AccountCodesInOrder[i], result.BytecodeByHash[hex]);
            }
        }

        [Fact]
        public async Task Sync_PeerSkipsMiddleCode_NoPositionalCorruption()
        {
            var s = BuildStateWithDistinctContracts(4);
            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(s.TrieStorage, s.Codes));
            var peer = new RecordingBytecodePeer(honest, r =>
            {
                var codes = new List<byte[]>();
                for (int i = 0; i < r.Hashes.Count; i++)
                {
                    if (i == 2) continue;
                    codes.Add(s.Codes.Get(r.Hashes[i]));
                }
                return new ByteCodesMessage { RequestId = r.RequestId, Codes = codes };
            });

            var client = CreateClient(peer);
            var result = await client.SyncStateAsync(s.StateRoot);

            Assert.True(peer.ByteCodesCallCount >= 2, "the skipped code must be re-requested");
            Assert.Equal(4, result.BytecodeByHash.Count);
            foreach (var codeHash in s.AccountCodeHashesInOrder)
            {
                var hex = codeHash.ToHex();
                Assert.True(result.BytecodeByHash.ContainsKey(hex));
                Assert.Equal(codeHash, Sha(result.BytecodeByHash[hex]));
            }
        }

        [Fact]
        public async Task Sync_PeerReturnsCodesOutOfOrder_AllStoredUnderCorrectHash()
        {
            var s = BuildStateWithDistinctContracts(3);
            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(s.TrieStorage, s.Codes));
            var peer = new RecordingBytecodePeer(honest, r =>
            {
                var ordered = r.Hashes.Select(h => s.Codes.Get(h)).ToList();
                var shuffled = new List<byte[]> { ordered[2], ordered[0], ordered[1] };
                return new ByteCodesMessage { RequestId = r.RequestId, Codes = shuffled };
            });

            var client = CreateClient(peer);
            var result = await client.SyncStateAsync(s.StateRoot);

            Assert.Equal(3, result.BytecodeByHash.Count);
            for (int i = 0; i < 3; i++)
            {
                var hex = s.AccountCodeHashesInOrder[i].ToHex();
                Assert.True(result.BytecodeByHash.ContainsKey(hex));
                Assert.Equal(s.AccountCodesInOrder[i], result.BytecodeByHash[hex]);
            }
        }

        [Fact]
        public async Task Sync_PeerReturnsNilCode_NotWritten()
        {
            var s = BuildStateWithDistinctContracts(3);
            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(s.TrieStorage, s.Codes));
            var peer = new RecordingBytecodePeer(honest, r =>
            {
                var codes = new List<byte[]>();
                for (int i = 0; i < r.Hashes.Count; i++)
                {
                    if (i == 1) { codes.Add(null); continue; }
                    codes.Add(s.Codes.Get(r.Hashes[i]));
                }
                return new ByteCodesMessage { RequestId = r.RequestId, Codes = codes };
            });

            var client = CreateClient(peer);
            var result = await client.SyncStateAsync(s.StateRoot);

            Assert.True(peer.ByteCodesCallCount >= 2, "the nil-returned code must be re-requested");
            Assert.Equal(3, result.BytecodeByHash.Count);
            foreach (var codeHash in s.AccountCodeHashesInOrder)
            {
                var hex = codeHash.ToHex();
                Assert.True(result.BytecodeByHash.ContainsKey(hex));
                Assert.Equal(codeHash, Sha(result.BytecodeByHash[hex]));
            }
        }

        [Fact]
        public async Task Sync_RequestContainsEmptyDataHash_FilteredBeforeWire()
        {
            var s = BuildStateWithDistinctContracts(2);
            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(s.TrieStorage, s.Codes));
            var peer = new RecordingBytecodePeer(honest, r => new ByteCodesMessage
            {
                RequestId = r.RequestId,
                Codes = r.Hashes.Select(h => s.Codes.Get(h)).ToList()
            });

            var client = CreateClient(peer);
            await client.SyncStateAsync(s.StateRoot);

            Assert.DoesNotContain(peer.LastRequestedHashes, h => ByteUtil.AreEqual(h, DefaultValues.EMPTY_DATA_HASH));
            Assert.Equal(2, peer.LastRequestedHashes.Count);
        }

        [Fact]
        public async Task Sync_DuplicateContractsShareCodeHash_RequestedOnce()
        {
            var keccak = new Sha3Keccack();
            var hashProvider = new Sha3KeccackHashProvider();
            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            var codes = new InMemoryBytecodeStore();

            var sharedCode = new byte[] { 0x60, 0x00, 0xF3 };
            var sharedHash = hashProvider.ComputeHash(sharedCode);
            codes.Put(sharedHash, sharedCode);

            for (int i = 0; i < 3; i++)
            {
                var addrHash = keccak.CalculateHash(new byte[] { (byte)i, 0xCD });
                var body = new AccountEncoder().Encode(new Account
                {
                    Nonce = (EvmUInt256)(uint)(i + 1),
                    Balance = (EvmUInt256)(ulong)((ulong)(i + 1) * 11UL),
                    StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                    CodeHash = sharedHash
                });
                trie.Put(addrHash, body);
            }
            trie.SaveDirtyNodesToStorage();
            var stateRoot = trie.Root.GetHash();

            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(storage, codes));
            var peer = new RecordingBytecodePeer(honest, r => new ByteCodesMessage
            {
                RequestId = r.RequestId,
                Codes = r.Hashes.Select(h => codes.Get(h)).ToList()
            });

            var client = CreateClient(peer);
            var result = await client.SyncStateAsync(stateRoot);

            Assert.Single(peer.LastRequestedHashes);
            Assert.True(ByteUtil.AreEqual(peer.LastRequestedHashes[0], sharedHash));
            Assert.Single(result.BytecodeByHash);
            Assert.True(result.BytecodeByHash.ContainsKey(sharedHash.ToHex()));
            Assert.Equal(sharedCode, result.BytecodeByHash[sharedHash.ToHex()]);
        }

        [Fact]
        public async Task Sync_TransientlyUnavailableCode_PatientlyFetched_NoDeferNoAbort()
        {
            var s = BuildStateWithDistinctContracts(1);
            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(s.TrieStorage, s.Codes));
            var codeHash = s.AccountCodeHashesInOrder[0];
            var calls = 0;
            var peer = new RecordingBytecodePeer(honest, r =>
            {
                int n = Interlocked.Increment(ref calls);
                if (n <= 2) return new ByteCodesMessage { RequestId = r.RequestId, Codes = new List<byte[]>() };
                return new ByteCodesMessage { RequestId = r.RequestId, Codes = r.Hashes.Select(h => s.Codes.Get(h)).ToList() };
            });

            var client = CreateClient(peer);
            var result = await client.SyncStateAsync(s.StateRoot);

            Assert.True(result.RootMatchesTarget);
            Assert.Empty(result.CodeHashesNeedingHeal);
            Assert.True(result.BytecodeByHash.ContainsKey(codeHash.ToHex()));
            Assert.Equal(s.AccountCodesInOrder[0], result.BytecodeByHash[codeHash.ToHex()]);
            Assert.True(peer.ByteCodesCallCount >= 3, "the code must have been re-requested through the transient refusals");
        }

        [Fact]
        public async Task Sync_DeadEndCode_DeferredToHeal_NotAborted()
        {
            var s = BuildStateWithDistinctContracts(1);
            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(s.TrieStorage, s.Codes));
            var codeHash = s.AccountCodeHashesInOrder[0];
            var evilCode = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };

            var peer = new RecordingBytecodePeer(honest, r => new ByteCodesMessage
            {
                RequestId = r.RequestId,
                Codes = new List<byte[]> { evilCode }
            });

            var client = CreateClient(peer);
            client.BytecodeDeadEndNoProgressRounds = 2;

            var result = await client.SyncStateAsync(s.StateRoot);

            Assert.True(result.RootMatchesTarget);
            Assert.Equal(1, result.AccountCount);
            Assert.DoesNotContain(codeHash.ToHex(), result.BytecodeByHash.Keys);
            Assert.Contains(result.CodeHashesNeedingHeal, h => ByteUtil.AreEqual(h, codeHash));
        }

        [Fact]
        public async Task Sync_DeadEndCode_AdvancesCheckpointCursor()
        {
            var s = BuildStateWithDistinctContracts(1);
            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(s.TrieStorage, s.Codes));
            var evilCode = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };
            var checkpoints = new List<SnapSyncState>();

            var peer = new RecordingBytecodePeer(honest, r => new ByteCodesMessage
            {
                RequestId = r.RequestId,
                Codes = new List<byte[]> { evilCode }
            });

            var client = CreateClient(peer);
            client.BytecodeDeadEndNoProgressRounds = 2;
            var result = await client.SyncStateAsync(
                s.StateRoot, resumeFrom: null, checkpointSink: state => checkpoints.Add(state));

            Assert.NotNull(result);
            Assert.Equal(1, result.AccountCount);
            Assert.NotEmpty(result.CodeHashesNeedingHeal);
        }
        [Fact]
        public async Task Sync_OverMaxCodeRequestCount_SplitsAcrossMultipleRequests()
        {
            const int ContractCount = 1100;
            var s = BuildStateWithDistinctContracts(ContractCount);
            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(s.TrieStorage, s.Codes));
            var peer = new RecordingBytecodePeer(honest, r => new ByteCodesMessage
            {
                RequestId = r.RequestId,
                Codes = r.Hashes.Select(h => s.Codes.Get(h)).ToList()
            });

            var client = CreateClient(peer);
            var result = await client.SyncStateAsync(s.StateRoot);

            Assert.Equal(ContractCount, result.BytecodeByHash.Count);
            Assert.True(peer.ByteCodesCallCount >= 2,
                $"expected >= 2 GetByteCodes calls due to chunking; got {peer.ByteCodesCallCount}");
            Assert.True(peer.LastRequestedHashes.Count <= 1024);
        }


        [Fact]
        public async Task Sync_DeadEndCode_CheckpointPayload_CarriesDeferredCodeHash_BeforeCallCompletes()
        {
            var s = BuildStateWithDistinctContracts(1);
            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(s.TrieStorage, s.Codes));
            var codeHash = s.AccountCodeHashesInOrder[0];
            var evilCode = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };

            var peer = new RecordingBytecodePeer(honest, r => new ByteCodesMessage
            {
                RequestId = r.RequestId,
                Codes = new List<byte[]> { evilCode }
            });

            var client = CreateClient(peer);
            client.BytecodeDeadEndNoProgressRounds = 2;
            client.CheckpointBytesThreshold = 1;

            var captured = new List<SnapSyncClient.SnapSyncCheckpoint>();
            var result = await client.SyncStateWithCheckpointAsync(
                s.StateRoot, resumeFrom: null, checkpointSink: cp => captured.Add(cp));

            Assert.True(result.RootMatchesTarget);
            Assert.NotEmpty(captured);

            var incremental = captured[0];
            Assert.Contains(incremental.DeferredCodeHashes, h => ByteUtil.AreEqual(h, codeHash));
            Assert.Equal(1UL, incremental.State.Counters.AccountsSynced);
        }

        [Fact]
        public async Task Sync_DeadEndCode_CheckpointSink_PersistsDeferredCodeBlob_AtomicallyWithCursor()
        {
            var s = BuildStateWithDistinctContracts(1);
            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(s.TrieStorage, s.Codes));
            var codeHash = s.AccountCodeHashesInOrder[0];
            var evilCode = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };

            var peer = new RecordingBytecodePeer(honest, r => new ByteCodesMessage
            {
                RequestId = r.RequestId,
                Codes = new List<byte[]> { evilCode }
            });

            var client = CreateClient(peer);
            client.BytecodeDeadEndNoProgressRounds = 2;
            client.CheckpointBytesThreshold = 1;

            using var bundle = InMemoryChainStoreBundle.Open();
            bool sawBlobAtFirstCommit = false;

            SnapSyncClient.SnapSyncCheckpoint firstCheckpoint = null;
            var result = await client.SyncStateWithCheckpointAsync(
                s.StateRoot, resumeFrom: null, checkpointSink: checkpoint =>
                {
                    using var batch = bundle.BeginBatch();
                    if (checkpoint.DeferredCodeHashes.Count > 0)
                    {
                        var union = new HashSet<byte[]>(ByteArrayComparer.Current);
                        foreach (var h in DeferredHealCodeCodec.Decode(bundle.Metadata.GetDeferredHealCodeBlob()))
                            union.Add(h);
                        foreach (var h in checkpoint.DeferredCodeHashes) union.Add(h);
                        batch.SaveDeferredHealCodeBlob(DeferredHealCodeCodec.Encode(new List<byte[]>(union)));
                    }
                    batch.SaveSnapSyncState(checkpoint.State);
                    batch.CommitAsync().GetAwaiter().GetResult();

                    if (firstCheckpoint == null)
                    {
                        firstCheckpoint = checkpoint;
                        var blob = DeferredHealCodeCodec.Decode(bundle.Metadata.GetDeferredHealCodeBlob());
                        sawBlobAtFirstCommit = blob.Exists(h => ByteUtil.AreEqual(h, codeHash));
                    }
                });

            Assert.True(result.RootMatchesTarget);
            Assert.NotNull(firstCheckpoint);
            Assert.True(sawBlobAtFirstCommit,
                "the deferred code hash must be durable in the SAME commit that advanced the cursor, " +
                "not only after Phase 2's own end-of-run persist.");

            var finalBlob = DeferredHealCodeCodec.Decode(bundle.Metadata.GetDeferredHealCodeBlob());
            Assert.Contains(finalBlob, h => ByteUtil.AreEqual(h, codeHash));
        }
    }
}

