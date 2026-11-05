using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.UnitTests.Sync;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.SecurityAudit71.S2.PartB
{
    public class S2SnapServingCoverageTests
    {
        private sealed class CountingBalStore : IBlockAccessListStore
        {
            public int Reads;
            public byte[] Payload = new byte[600_000];
            public Task SaveAsync(byte[] blockHash, byte[] rlp) => Task.CompletedTask;
            public Task<byte[]> GetByBlockHashAsync(byte[] blockHash) { Reads++; return Task.FromResult(Payload); }
            public Task<byte[]> GetByBlockNumberAsync(BigInteger n) => Task.FromResult<byte[]>(null);
            public Task DeleteByBlockHashAsync(byte[] blockHash) => Task.CompletedTask;
            public Task DeleteByBlockNumberAsync(BigInteger n) => Task.CompletedTask;
        }

        private static List<byte[]> Hashes(int n) => Enumerable.Range(0, n).Select(i => { var h = new byte[32]; h[0] = (byte)i; h[1] = (byte)(i >> 8); return h; }).ToList();

        [Fact]
        public async Task S2_D1_Given_AByteCodesRequestWithASmallResponseBytes_When_Served_Then_TheByteBudgetStopsTheResponse()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 4, withCode: 0);
            var hp = new Sha3KeccackHashProvider();
            var hashes = new List<byte[]>();
            for (var i = 0; i < 10; i++)
            {
                var code = Enumerable.Repeat((byte)(i + 1), 1000).ToArray();
                var h = hp.ComputeHash(code);
                f.Bytecodes.Put(h, code);
                hashes.Add(h);
            }
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var resp = await handler.GetByteCodesAsync(new GetByteCodesMessage { RequestId = 1, Hashes = hashes, ResponseBytes = 1500 });

            Assert.True(resp.Codes.Count <= 2, $"served {resp.Codes.Count} codes of 1000 bytes against a 1500 byte budget");
        }

        [Fact]
        public async Task S2_D1_Given_ATrieNodesRequestWithASmallResponseBytes_When_Served_Then_TheByteBudgetStopsTheResponse()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 64, withCode: 0);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);
            var paths = Enumerable.Range(0, 50).Select(_ => new List<byte[]> { new byte[0] }).ToList();

            var resp = await handler.GetTrieNodesAsync(new GetTrieNodesMessage { RequestId = 1, RootHash = f.StateTrie.Root.GetHash(), Paths = paths, ResponseBytes = 100 });

            Assert.True(resp.Nodes.Count <= 2, $"served {resp.Nodes.Count} root nodes against a 100 byte budget");
        }

        [Fact]
        public async Task S2_D3_Given_AnAccountRangeForARootTheNodeDoesNotHold_When_Served_Then_AnEmptyReplyIsReturnedNotAnException()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 4, withCode: 0);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var resp = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 1,
                RootHash = SnapHandlerTestFixture.FilledHash(0x77),
                StartingHash = new byte[32],
                LimitHash = SnapHandlerTestFixture.FilledHash(0xff),
                ResponseBytes = 100_000
            });

            Assert.Empty(resp.Accounts);
        }

        [Fact]
        public async Task S2_D1_Given_AGetBlockAccessListsOfManyHashes_When_Served_Then_TheLookupCountIsCapped()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 2, withCode: 0);
            var bal = new CountingBalStore { Payload = new byte[1] };
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes, blockAccessLists: bal);

            await handler.GetBlockAccessListsAsync(new GetBlockAccessListsMessage { RequestId = 1, Hashes = Hashes(5000), Bytes = ulong.MaxValue });

            Assert.True(bal.Reads <= PatriciaSnapRequestHandler.MaxBlockAccessListLookups, $"{bal.Reads} BAL reads");
        }

        [Fact]
        public async Task S2_D1_Given_AGetBlockAccessListsWithASmallByteBudget_When_Served_Then_TheByteBudgetStopsTheResponse()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 2, withCode: 0);
            var bal = new CountingBalStore();
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes, blockAccessLists: bal);

            var resp = await handler.GetBlockAccessListsAsync(new GetBlockAccessListsMessage { RequestId = 1, Hashes = Hashes(100), Bytes = 700_000 });

            Assert.True(resp.BlockAccessListsByBlock.Count <= 2, $"served {resp.BlockAccessListsByBlock.Count} BALs of 600000 bytes against a 700000 byte budget");
        }
    }
}
