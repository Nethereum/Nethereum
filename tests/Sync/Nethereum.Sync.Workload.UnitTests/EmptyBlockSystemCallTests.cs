using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.Chain.TestData.Vectors;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    /// <summary>
    /// EIP-2935, "Block processing": "At the start of processing any block where this EIP is active
    /// (ie. before processing any transactions), call to HISTORY_STORAGE_ADDRESS as SYSTEM_ADDRESS with
    /// the 32-byte input of block.parent.hash". A block with no transactions therefore still changes
    /// state, which is why a follower cannot treat an empty tail as a no-change region.
    /// </summary>
    public class EmptyBlockSystemCallTests
    {
        private static BigInteger Num(byte[] b) => b == null ? BigInteger.Zero : new BigInteger(b.Reverse().Concat(new byte[] { 0 }).ToArray());

        [Fact]
        public async Task Given_AnEmptyBlockUnderPrague_When_ItIsExecuted_Then_TheSystemPredeployStorageStillChanges()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 2);
            await sequencer.ProduceBlockAsync();

            var before = await sequencer.State.GetAllStorageAsync(Eip2935Constants.HistoryStorageAddress);

            await sequencer.ProduceBlockAsync();

            var height = await sequencer.Blocks.GetHeightAsync();
            var parentHash = await sequencer.Blocks.GetHashByNumberAsync(height - 1);
            Assert.Empty(await sequencer.Transactions.GetHashesByBlockHashAsync(
                await sequencer.Blocks.GetHashByNumberAsync(height)));

            var after = await sequencer.State.GetAllStorageAsync(Eip2935Constants.HistoryStorageAddress);

            Assert.DoesNotContain(before, kv => Num(kv.Value) == Num(parentHash));
            Assert.Contains(after, kv => Num(kv.Value) == Num(parentHash));
            Assert.Equal(before.Count + 1, after.Count);
        }
    }
}
