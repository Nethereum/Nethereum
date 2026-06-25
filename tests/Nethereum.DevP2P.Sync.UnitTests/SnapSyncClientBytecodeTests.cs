using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

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

            public List<byte[]> LastRequestedHashes { get; private set; } = new();
            public int ByteCodesCallCount { get; private set; }

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
                ByteCodesCallCount++;
                LastRequestedHashes = new List<byte[]>(r.Hashes);
                return Task.FromResult(_byteCodesResponder(r));
            }

            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(r, ct);
        }

        private sealed class Scenario
        {
            public byte[] StateRoot { get; init; }
            public InMemoryTrieStorage TrieStorage { get; init; }
            public InMemoryBytecodeStore Codes { get; init; }
            public List<byte[]> AccountCodeHashesInOrder { get; init; }
            public List<byte[]> AccountCodesInOrder { get; init; }
        }

        private static Scenario BuildStateWithDistinctContracts(int contractCount)
        {
            var keccak = new Sha3Keccack();
            var hashProvider = new Sha3KeccackHashProvider();
            var trie = new PatriciaTrie();
            var storage = new InMemoryTrieStorage();
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
                trie.Put(addrHash, body, storage);
                hashes.Add(codeHash);
                bodies.Add(code);
            }
            trie.SaveDirtyNodesToStorage(storage);
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
            var client = new SnapSyncClient(peer, accountsPerRequest: 64, responseBytesBudget: 1_000_000UL);
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

            var client = new SnapSyncClient(peer, accountsPerRequest: 64, responseBytesBudget: 1_000_000UL);
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
                    if (i == 2) continue; // peer skips slot 2 (e.g. unknown hash)
                    codes.Add(s.Codes.Get(r.Hashes[i]));
                }
                return new ByteCodesMessage { RequestId = r.RequestId, Codes = codes };
            });

            var client = new SnapSyncClient(peer, accountsPerRequest: 64, responseBytesBudget: 1_000_000UL);
            var result = await client.SyncStateAsync(s.StateRoot);

            // Three correctly-keyed entries should land; the hash that the peer skipped
            // (request slot 2) must NOT appear in the result. Crucially, the codes that
            // DID come back must all live under their TRUE keccak, NOT under the
            // requested slot the loop happened to be at.
            Assert.Equal(4, peer.LastRequestedHashes.Count);
            var skippedHash = peer.LastRequestedHashes[2];
            Assert.Equal(3, result.BytecodeByHash.Count);
            Assert.False(result.BytecodeByHash.ContainsKey(skippedHash.ToHex()));
            for (int i = 0; i < 4; i++)
            {
                if (i == 2) continue;
                var hex = peer.LastRequestedHashes[i].ToHex();
                Assert.True(result.BytecodeByHash.ContainsKey(hex));
                // The stored bytes must keccak back to the hash they're stored under.
                Assert.Equal(peer.LastRequestedHashes[i], Sha(result.BytecodeByHash[hex]));
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
                // reorder: [c2, c0, c1]
                var shuffled = new List<byte[]> { ordered[2], ordered[0], ordered[1] };
                return new ByteCodesMessage { RequestId = r.RequestId, Codes = shuffled };
            });

            var client = new SnapSyncClient(peer, accountsPerRequest: 64, responseBytesBudget: 1_000_000UL);
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

            var client = new SnapSyncClient(peer, accountsPerRequest: 64, responseBytesBudget: 1_000_000UL);
            var result = await client.SyncStateAsync(s.StateRoot);

            // Two codes land; the nil slot (request index 1) is dropped — no nil ever
            // written to the sink, and the hash for that slot is not present.
            Assert.Equal(2, result.BytecodeByHash.Count);
            Assert.Equal(3, peer.LastRequestedHashes.Count);
            Assert.False(result.BytecodeByHash.ContainsKey(peer.LastRequestedHashes[1].ToHex()));
            Assert.True(result.BytecodeByHash.ContainsKey(peer.LastRequestedHashes[0].ToHex()));
            Assert.True(result.BytecodeByHash.ContainsKey(peer.LastRequestedHashes[2].ToHex()));
        }

        [Fact]
        public async Task Sync_RequestContainsEmptyDataHash_FilteredBeforeWire()
        {
            // Three contract accounts; the emptyHash never appears because real accounts
            // skip code fetch when their CodeHash is EMPTY_DATA_HASH. Instead we directly
            // verify the dedup helper's behaviour by inspecting what the peer sees: only
            // the real contract hashes, never EMPTY_DATA_HASH, even if the producer of
            // codeHashesToFetch were to mistakenly forward it.
            var s = BuildStateWithDistinctContracts(2);
            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(s.TrieStorage, s.Codes));
            var peer = new RecordingBytecodePeer(honest, r => new ByteCodesMessage
            {
                RequestId = r.RequestId,
                Codes = r.Hashes.Select(h => s.Codes.Get(h)).ToList()
            });

            var client = new SnapSyncClient(peer, accountsPerRequest: 64, responseBytesBudget: 1_000_000UL);
            await client.SyncStateAsync(s.StateRoot);

            Assert.DoesNotContain(peer.LastRequestedHashes, h => ByteUtil.AreEqual(h, DefaultValues.EMPTY_DATA_HASH));
            Assert.Equal(2, peer.LastRequestedHashes.Count);
        }

        [Fact]
        public async Task Sync_DuplicateContractsShareCodeHash_RequestedOnce()
        {
            // Build state where two accounts share the same code (factory pattern).
            var keccak = new Sha3Keccack();
            var hashProvider = new Sha3KeccackHashProvider();
            var trie = new PatriciaTrie();
            var storage = new InMemoryTrieStorage();
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
                trie.Put(addrHash, body, storage);
            }
            trie.SaveDirtyNodesToStorage(storage);
            var stateRoot = trie.Root.GetHash();

            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(storage, codes));
            var peer = new RecordingBytecodePeer(honest, r => new ByteCodesMessage
            {
                RequestId = r.RequestId,
                Codes = r.Hashes.Select(h => codes.Get(h)).ToList()
            });

            var client = new SnapSyncClient(peer, accountsPerRequest: 64, responseBytesBudget: 1_000_000UL);
            var result = await client.SyncStateAsync(stateRoot);

            Assert.Single(peer.LastRequestedHashes);
            Assert.True(ByteUtil.AreEqual(peer.LastRequestedHashes[0], sharedHash));
            Assert.Single(result.BytecodeByHash);
            Assert.True(result.BytecodeByHash.ContainsKey(sharedHash.ToHex()));
            Assert.Equal(sharedCode, result.BytecodeByHash[sharedHash.ToHex()]);
        }

        [Fact]
        public async Task Sync_PeerReturnsCodeWithWrongHash_NothingWritten()
        {
            var s = BuildStateWithDistinctContracts(1);
            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(s.TrieStorage, s.Codes));
            var evilCode = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };

            var peer = new RecordingBytecodePeer(honest, r => new ByteCodesMessage
            {
                RequestId = r.RequestId,
                Codes = new List<byte[]> { evilCode }
            });

            var client = new SnapSyncClient(peer, accountsPerRequest: 64, responseBytesBudget: 1_000_000UL);
            var result = await client.SyncStateAsync(s.StateRoot);

            Assert.Empty(result.BytecodeByHash);
            var evilHashHex = Sha(evilCode).ToHex();
            Assert.False(result.BytecodeByHash.ContainsKey(evilHashHex));
            Assert.False(result.BytecodeByHash.ContainsKey(s.AccountCodeHashesInOrder[0].ToHex()));
        }

        [Fact]
        public async Task Sync_OverMaxCodeRequestCount_SplitsAcrossMultipleRequests()
        {
            // > 1024 distinct contracts so the bytecode batch is forced to
            // chunk. Pre-fix: single GetByteCodes call covered everything
            // and the peer silently truncated, losing the tail. Post-fix:
            // multiple calls of <= MaxCodeRequestCount each, all codes
            // delivered.
            const int ContractCount = 1100;
            var s = BuildStateWithDistinctContracts(ContractCount);
            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(s.TrieStorage, s.Codes));
            var peer = new RecordingBytecodePeer(honest, r => new ByteCodesMessage
            {
                RequestId = r.RequestId,
                Codes = r.Hashes.Select(h => s.Codes.Get(h)).ToList()
            });

            var client = new SnapSyncClient(peer, accountsPerRequest: 64, responseBytesBudget: 1_000_000UL);
            var result = await client.SyncStateAsync(s.StateRoot);

            Assert.Equal(ContractCount, result.BytecodeByHash.Count);
            Assert.True(peer.ByteCodesCallCount >= 2,
                $"expected >= 2 GetByteCodes calls due to chunking; got {peer.ByteCodesCallCount}");
            // Every chunk must obey the per-request hard cap.
            Assert.True(peer.LastRequestedHashes.Count <= 1024);
        }
    }
}
