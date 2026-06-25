using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Sync
{
    /// <summary>
    /// Per-handler count + slack caps for snap/1
    /// (https://github.com/ethereum/devp2p/blob/master/caps/snap.md).
    /// Geth interop invariants pinned:
    /// <see cref="PatriciaSnapRequestHandler.MaxCodeLookups"/>,
    /// <see cref="PatriciaSnapRequestHandler.MaxTrieNodeLookups"/>,
    /// <see cref="PatriciaSnapRequestHandler.StateLookupSlack"/>.
    /// </summary>
    public class SnapHandlerCapTests
    {
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
