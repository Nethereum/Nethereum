using System.Numerics;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Serving.Strategies;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.SecurityAudit71.S2.PartB
{
    public class S2EthBalServingCapTests
    {
        private sealed class BigBalStore : IBlockAccessListStore
        {
            public Task SaveAsync(byte[] blockHash, byte[] rlp) => Task.CompletedTask;
            public Task<byte[]> GetByBlockHashAsync(byte[] blockHash) => Task.FromResult(new byte[600_000]);
            public Task<byte[]> GetByBlockNumberAsync(BigInteger n) => Task.FromResult<byte[]>(null!);
            public Task DeleteByBlockHashAsync(byte[] blockHash) => Task.CompletedTask;
            public Task DeleteByBlockNumberAsync(BigInteger n) => Task.CompletedTask;
        }

        [Fact]
        public async Task S2_D2_Given_ManyLargeBlockAccessLists_When_ServedOverEth71_Then_TheResponseStopsAtTheTwoMebibyteCap()
        {
            var handler = new StorageBackedEth68Handler(null!, null!, null!, new BigBalStore(), null!, null!, null!);
            var hashes = Enumerable.Range(0, 20).Select(i => { var h = new byte[32]; h[0] = (byte)i; return h; }).ToArray();

            var served = await handler.GetBlockAccessListsAsync(hashes);

            Assert.True(served.Count <= 4, $"served {served.Count} entries of 600000 bytes");
        }
    }
}
