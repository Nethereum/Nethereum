using System.Threading.Tasks;
using Nethereum.Chain.TestData.Vectors;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class SequencerDeterminismTests
    {
        [Fact]
        public async Task TwoSequencers_SameVector_ProduceIdenticalBlocks()
        {
            var a = await InProcessSequencerDriver.CreateAsync();
            await new WorkloadV1().BuildAsync(a);

            var b = await InProcessSequencerDriver.CreateAsync();
            await new WorkloadV1().BuildAsync(b);

            var height = (int)(await a.Blocks.GetHeightAsync());
            Assert.Equal(height, (int)(await b.Blocks.GetHeightAsync()));
            for (var n = 0; n <= height; n++)
                Assert.Equal(await a.Blocks.GetHashByNumberAsync(n), await b.Blocks.GetHashByNumberAsync(n));
        }
    }
}
