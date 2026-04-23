using Nethereum.Documentation;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class BlobGasCalculatorTests
    {
        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "eip4844-blob-gas", "Calculate blob base fee from excess blob gas")]
        public void ShouldCalculateBlobBaseFeeAtZeroExcess()
        {
            var fee = BlobGasCalculator.CalculateBlobBaseFee(EvmUInt256.Zero);
            Assert.Equal(EvmUInt256.One, fee);
        }

        [Fact]
        public void ShouldReturnMinFeeAtLowExcess()
        {
            var fee = BlobGasCalculator.CalculateBlobBaseFee(
                new EvmUInt256((ulong)BlobGasCalculator.TARGET_BLOB_GAS_PER_BLOCK));
            Assert.Equal(EvmUInt256.One, fee);
        }

        [Fact]
        public void ShouldIncreaseFeeAboveUpdateFraction()
        {
            var fee1 = BlobGasCalculator.CalculateBlobBaseFee(EvmUInt256.Zero);
            var fee2 = BlobGasCalculator.CalculateBlobBaseFee(
                new EvmUInt256((ulong)BlobGasCalculator.BLOB_BASE_FEE_UPDATE_FRACTION));
            var fee3 = BlobGasCalculator.CalculateBlobBaseFee(
                new EvmUInt256((ulong)BlobGasCalculator.BLOB_BASE_FEE_UPDATE_FRACTION * 3));
            var fee4 = BlobGasCalculator.CalculateBlobBaseFee(
                new EvmUInt256((ulong)BlobGasCalculator.BLOB_BASE_FEE_UPDATE_FRACTION * 10));

            Assert.Equal(EvmUInt256.One, fee1);
            Assert.True(fee2 > fee1, $"fee at 1x fraction ({fee2}) should be > min ({fee1})");
            Assert.True(fee3 > fee2, $"fee at 3x fraction ({fee3}) should be > 1x ({fee2})");
            Assert.True(fee4 > fee3, $"fee at 10x fraction ({fee4}) should be > 3x ({fee3})");
        }

        [Fact]
        public void ShouldCalculateBlobGasCost()
        {
            var baseFee = new EvmUInt256(10);
            var cost = BlobGasCalculator.CalculateBlobGasCost(3, baseFee);
            Assert.Equal(new EvmUInt256(3 * 131072 * 10UL), cost);
        }

        [Fact]
        public void ShouldSuggestMaxFeePerBlobGas()
        {
            var suggested = BlobGasCalculator.SuggestMaxFeePerBlobGas(EvmUInt256.Zero);
            Assert.Equal(new EvmUInt256(2), suggested);

            var suggestedHigh = BlobGasCalculator.SuggestMaxFeePerBlobGas(
                new EvmUInt256((ulong)BlobGasCalculator.BLOB_BASE_FEE_UPDATE_FRACTION * 5));
            Assert.True(suggestedHigh > suggested);
        }

        [Fact]
        public void ShouldHaveCorrectConstants()
        {
            Assert.Equal(131072, BlobGasCalculator.GAS_PER_BLOB);
            Assert.Equal(786432, BlobGasCalculator.MAX_BLOB_GAS_PER_BLOCK);
            Assert.Equal(393216, BlobGasCalculator.TARGET_BLOB_GAS_PER_BLOCK);
            Assert.Equal(3338477, BlobGasCalculator.BLOB_BASE_FEE_UPDATE_FRACTION);
        }
    }
}
