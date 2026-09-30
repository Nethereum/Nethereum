using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class IncrementalCalculatorColdCacheStorageTests
    {
        private const string C = "0x00000000000000000000000000000000000000ca";

        [Fact]
        public async Task IncrementalStorageWrite_OnColdCache_PreservesPriorSlots_MatchesFullRebuild()
        {
            var state = new InMemoryStateStore();
            var nodeStore = new InMemoryContentNodeStore();

            await state.SaveAccountAsync(C, new Account { Nonce = 1, Balance = 0, CodeHash = new byte[] { 1, 2, 3 } });
            await state.SaveStorageAsync(C, 1, new byte[] { 0x11 });
            await state.SaveStorageAsync(C, 2, new byte[] { 0x22 });
            await state.SaveStorageAsync(C, 3, new byte[] { 0x33 });

            var calc1 = new IncrementalStateRootCalculator(state, nodeStore);
            var r1 = await calc1.ComputeStateRootAsync();

            var calc2 = new IncrementalStateRootCalculator(state, nodeStore);
            var warm = await calc2.ComputeStateRootAsync(r1);
            Assert.Equal(r1, warm);

            await state.SaveStorageAsync(C, 1, new byte[] { 0x99 });
            var r2 = await calc2.ComputeStateRootAsync();

            var calc3 = new IncrementalStateRootCalculator(state, new InMemoryContentNodeStore());
            var r2Full = await calc3.ComputeFullStateRootAsync();

            Assert.Equal(r2Full, r2);
        }
    }
}
