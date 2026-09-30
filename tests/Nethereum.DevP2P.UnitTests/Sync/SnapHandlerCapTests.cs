using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;

namespace Nethereum.DevP2P.UnitTests.Sync
{
    public class SnapHandlerCapTests
    {
        private sealed class CountingBytecodeStore : IBytecodeStore
        {
            private readonly IBytecodeStore _inner;
            public int GetCallCount;
            public CountingBytecodeStore(IBytecodeStore inner) { _inner = inner; }
            public byte[] Get(byte[] codeHash) { GetCallCount++; return _inner.Get(codeHash); }
        }

        [Fact]
        public async Task Given_PeerFloodsEmptyDataHash_When_ByteCodesServed_Then_CountCappedAtMaxCodeLookups()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 4, withCode: 0);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var hashes = Enumerable.Range(0, 5000)
                .Select(_ => DefaultValues.EMPTY_DATA_HASH)
                .ToList();

            var resp = await handler.GetByteCodesAsync(new GetByteCodesMessage
            {
                RequestId = 1,
                Hashes = hashes,
                ResponseBytes = ulong.MaxValue
            });

            Assert.True(resp.Codes.Count <= PatriciaSnapRequestHandler.MaxCodeLookups,
                $"resp.Codes.Count {resp.Codes.Count} exceeded MaxCodeLookups {PatriciaSnapRequestHandler.MaxCodeLookups}");
        }

        [Fact]
        public async Task Given_AllMissByteCodesRequestBeyondCap_When_ByteCodesServed_Then_StoreLookupsBoundedAtMaxCodeLookups()
        {
            var counting = new CountingBytecodeStore(new SnapHandlerTestFixture.InMemoryBytecodeStore());
            var handler = new PatriciaSnapRequestHandler(new InMemoryContentNodeStore(), counting);

            var hashes = new List<byte[]>();
            for (int i = 0; i < 5000; i++)
            {
                var h = new byte[32];
                h[0] = (byte)(i & 0xff);
                h[1] = (byte)((i >> 8) & 0xff);
                h[31] = 0x01;
                hashes.Add(h);
            }

            var resp = await handler.GetByteCodesAsync(new GetByteCodesMessage
            {
                RequestId = 9,
                Hashes = hashes,
                ResponseBytes = ulong.MaxValue
            });

            Assert.Empty(resp.Codes);
            Assert.True(counting.GetCallCount <= PatriciaSnapRequestHandler.MaxCodeLookups,
                $"store.Get was called {counting.GetCallCount} times, exceeding MaxCodeLookups {PatriciaSnapRequestHandler.MaxCodeLookups}");
        }

        [Fact]
        public async Task Given_HonestByteCodesRequestAtCapAllHits_When_ByteCodesServed_Then_AllCodesReturned()
        {
            var store = new SnapHandlerTestFixture.InMemoryBytecodeStore();
            var hashProvider = new Sha3KeccackHashProvider();
            var hashes = new List<byte[]>();
            for (int i = 0; i < PatriciaSnapRequestHandler.MaxCodeLookups; i++)
            {
                var code = new byte[] { (byte)(i & 0xff), (byte)((i >> 8) & 0xff), 0xEE };
                var hash = hashProvider.ComputeHash(code);
                store.Put(hash, code);
                hashes.Add(hash);
            }

            var handler = new PatriciaSnapRequestHandler(new InMemoryContentNodeStore(), store);
            var resp = await handler.GetByteCodesAsync(new GetByteCodesMessage
            {
                RequestId = 10,
                Hashes = hashes,
                ResponseBytes = ulong.MaxValue
            });

            Assert.Equal(PatriciaSnapRequestHandler.MaxCodeLookups, resp.Codes.Count);
        }

        [Fact]
        public async Task Given_PeerSendsManyPathsetsWithUnknownAccounts_When_TrieNodesServed_Then_AccountResolutionAttemptsBoundedAtMaxTrieNodeLookups()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 16);
            int resolveAttempts = 0;
            Func<byte[], byte[]> spy = _ => { resolveAttempts++; return null; };
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes, spy);

            var pathsets = new List<List<byte[]>>();
            for (int i = 0; i < 5000; i++)
            {
                var unknownAccountHash = new byte[32];
                unknownAccountHash[0] = (byte)(i & 0xff);
                unknownAccountHash[1] = (byte)((i >> 8) & 0xff);
                unknownAccountHash[31] = 0x02;
                pathsets.Add(new List<byte[]> { unknownAccountHash, new byte[] { 0x00 } });
            }

            var resp = await handler.GetTrieNodesAsync(new GetTrieNodesMessage
            {
                RequestId = 11,
                RootHash = f.StateTrie.Root.GetHash(),
                Paths = pathsets,
                ResponseBytes = ulong.MaxValue
            });

            Assert.Empty(resp.Nodes);
            Assert.True(resolveAttempts <= PatriciaSnapRequestHandler.MaxTrieNodeLookups,
                $"account-resolution attempts {resolveAttempts} exceeded MaxTrieNodeLookups {PatriciaSnapRequestHandler.MaxTrieNodeLookups}");
        }

        [Fact]
        public async Task Given_ZeroItemPathset_When_TrieNodesServed_Then_ThrowsSnapMalformedRequestException()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 4);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var pathsets = new List<List<byte[]>> { new List<byte[]>() };

            await Assert.ThrowsAsync<PatriciaSnapRequestHandler.SnapMalformedRequestException>(() =>
                handler.GetTrieNodesAsync(new GetTrieNodesMessage
                {
                    RequestId = 12,
                    RootHash = f.StateTrie.Root.GetHash(),
                    Paths = pathsets,
                    ResponseBytes = ulong.MaxValue
                }));
        }

        [Fact]
        public async Task Given_PeerSendsManyPathsetsSingleElement_When_TrieNodesServed_Then_CountCappedAtMaxTrieNodeLookups()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 16);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var pathsets = new List<List<byte[]>>();
            for (int i = 0; i < 5000; i++)
                pathsets.Add(new List<byte[]> { new byte[] { 0x00 } });

            var resp = await handler.GetTrieNodesAsync(new GetTrieNodesMessage
            {
                RequestId = 2,
                RootHash = f.StateTrie.Root.GetHash(),
                Paths = pathsets,
                ResponseBytes = ulong.MaxValue
            });

            Assert.True(resp.Nodes.Count <= PatriciaSnapRequestHandler.MaxTrieNodeLookups,
                $"resp.Nodes.Count {resp.Nodes.Count} exceeded MaxTrieNodeLookups {PatriciaSnapRequestHandler.MaxTrieNodeLookups}");
        }

        [Fact]
        public async Task Given_PeerSendsOneOversizedPathset_When_TrieNodesServed_Then_InnerLoopAlsoCappedAtMaxTrieNodeLookups()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 16);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var accountHash = f.Accounts[0].hash;
            var inner = new List<byte[]> { accountHash };
            for (int i = 0; i < 5000; i++)
                inner.Add(new byte[] { 0x00 });

            var resp = await handler.GetTrieNodesAsync(new GetTrieNodesMessage
            {
                RequestId = 3,
                RootHash = f.StateTrie.Root.GetHash(),
                Paths = new List<List<byte[]>> { inner },
                ResponseBytes = ulong.MaxValue
            });

            Assert.True(resp.Nodes.Count <= PatriciaSnapRequestHandler.MaxTrieNodeLookups,
                $"resp.Nodes.Count {resp.Nodes.Count} exceeded MaxTrieNodeLookups (inner-loop bound)");
        }

        [Fact]
        public async Task Given_PeerRequestsBelowSoftCap_When_StorageRangesServed_Then_HardLimitAppliesStateLookupSlack()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 8);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var resp = await handler.GetStorageRangesAsync(new GetStorageRangesMessage
            {
                RequestId = 4,
                RootHash = f.StateTrie.Root.GetHash(),
                AccountHashes = f.Accounts.Select(a => a.hash).ToList(),
                StartingHash = new byte[32],
                LimitHash = SnapHandlerTestFixture.FilledHash(0xff),
                ResponseBytes = 4096UL
            });

            var totalSlotBytes = resp.Slots.Sum(slots => slots.Sum(s => 32 + s.Data.Length));
            var hardLimit = (long)(4096 * (1.0 + PatriciaSnapRequestHandler.StateLookupSlack));
            Assert.True(totalSlotBytes <= hardLimit + 256,
                $"totalSlotBytes {totalSlotBytes} exceeded request hard limit {hardLimit} + overshoot");
        }
    }
}
