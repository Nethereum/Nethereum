using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Serving.Strategies;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    /// <summary>
    /// AMS-8159-03. What the eth/71 serve path does about a requested block whose
    /// access list it cannot answer.
    ///
    /// <para>EIP-8159 §BlockAccessLists (0x13): <i>"The RLP empty string
    /// (<c>0x80</c>) is returned for blocks where the BAL is unavailable."</i> The
    /// response is positional for its whole length, so an unavailable list keeps
    /// its slot and the walk continues.</para>
    ///
    /// <para>geth stops at the first hash it cannot answer and serves a prefix. The
    /// handler's own doc comment used to instruct exactly that, which is what these
    /// tests exist to keep out — geth is a reference, not the specification.</para>
    /// </summary>
    public class StorageBackedEth68HandlerBlockAccessListsTests
    {
        private static byte[] HashOf(byte seed)
        {
            var hash = new byte[32];
            hash[31] = seed;
            return hash;
        }

        private static byte[] BalOf(byte seed) => new byte[] { 0xC1, seed };

        private static async Task<StorageBackedEth68Handler> HandlerServingAsync(
            params byte[] seedsWithABal)
        {
            var blockAccessLists = new InMemoryBlockAccessListStore();
            foreach (var seed in seedsWithABal)
                await blockAccessLists.SaveAsync(HashOf(seed), BalOf(seed));

            return new StorageBackedEth68Handler(
                new InMemoryBlockStore(),
                new InMemoryTransactionStore(),
                new InMemoryReceiptStore(),
                blockAccessLists,
                null,
                null,
                null);
        }

        [Fact]
        [Trait("Category", "EIP8159")]
        public async Task Given_AHashTheStoreCannotAnswer_When_ServingBlockAccessLists_Then_ItsSlotIsEmptyAndLaterBlocksKeepTheirPosition()
        {
            var handler = await HandlerServingAsync((byte)1, (byte)3);

            var served = await handler.GetBlockAccessListsAsync(
                new[] { HashOf(1), HashOf(2), HashOf(3) });

            Assert.Equal(3, served.Count);
            Assert.Equal(BalOf(1), served[0]);
            Assert.Empty(served[1]);
            Assert.Equal(BalOf(3), served[2]);
        }

        [Fact]
        [Trait("Category", "EIP8159")]
        public async Task Given_EveryRequestedHashIsInTheStore_When_ServingBlockAccessLists_Then_NoSlotIsEmpty()
        {
            var handler = await HandlerServingAsync((byte)1, (byte)2);

            var served = await handler.GetBlockAccessListsAsync(new[] { HashOf(1), HashOf(2) });

            Assert.Equal(new List<byte[]> { BalOf(1), BalOf(2) }, served);
        }

        [Fact]
        [Trait("Category", "EIP8159")]
        public async Task Given_NoBlockAccessListStore_When_ServingBlockAccessLists_Then_NothingIsServed()
        {
            var handler = new StorageBackedEth68Handler(
                new InMemoryBlockStore(), new InMemoryTransactionStore(), new InMemoryReceiptStore());

            Assert.Empty(await handler.GetBlockAccessListsAsync(new[] { HashOf(1) }));
        }
    }
}
