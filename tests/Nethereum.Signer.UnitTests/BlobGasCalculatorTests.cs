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
            Assert.Equal(8192, BlobGasCalculator.BLOB_BASE_COST);
        }

        [Fact]
        public void Given_SumBelowTarget_When_CalculatingExcessBlobGas_Then_ReturnsZeroEvenWithAReservePriceThatWouldOtherwiseTrigger()
        {
            var reservePrice = new Eip7918ReservePriceInputs(
                parentBaseFeePerGas: new EvmUInt256(1_000_000_000UL),
                maxBlobsPerBlock: 9, targetBlobsPerBlock: 6,
                baseFeeUpdateFraction: new EvmUInt256(5_007_716UL));

            var result = BlobGasCalculator.CalculateExcessBlobGas(
                parentExcessBlobGas: 0, parentBlobGasUsed: 1, targetBlobGasPerBlock: 786432, reservePrice);

            Assert.Equal(0UL, result);
        }

        [Fact]
        public void Given_PreOsakaFork_When_CalculatingExcessBlobGas_Then_TheEip7918OverloadMatchesTheOriginalFormula()
        {
            const ulong parentExcessBlobGas = 0, parentBlobGasUsed = 917504, targetBlobGasPerBlock = 786432;

            var withoutReservePrice = BlobGasCalculator.CalculateExcessBlobGas(
                parentExcessBlobGas, parentBlobGasUsed, targetBlobGasPerBlock);
            var withNullReservePrice = BlobGasCalculator.CalculateExcessBlobGas(
                parentExcessBlobGas, parentBlobGasUsed, targetBlobGasPerBlock, reservePrice: null);

            Assert.Equal(131072UL, withoutReservePrice);
            Assert.Equal(withoutReservePrice, withNullReservePrice);
        }

        [Fact]
        public void Given_OsakaWithBlobDemandAboveTheReservePrice_When_CalculatingExcessBlobGas_Then_TargetIsSubtractedNormally()
        {
            // EIP-7918: "if BLOB_BASE_COST * parent.base_fee_per_gas > GAS_PER_BLOB * get_base_fee_per_blob_gas(parent)"
            // parent excess = 0 => get_base_fee_per_blob_gas(parent) == 1 => the trigger threshold is
            // base_fee_per_gas > GAS_PER_BLOB / BLOB_BASE_COST == 16. baseFeePerGas = 1 stays well under it.
            var reservePrice = new Eip7918ReservePriceInputs(
                parentBaseFeePerGas: new EvmUInt256(1UL),
                maxBlobsPerBlock: 9, targetBlobsPerBlock: 6,
                baseFeeUpdateFraction: new EvmUInt256(5_007_716UL));

            var result = BlobGasCalculator.CalculateExcessBlobGas(
                parentExcessBlobGas: 0, parentBlobGasUsed: 917504, targetBlobGasPerBlock: 786432, reservePrice);

            Assert.Equal(131072UL, result);
        }

        [Fact]
        public void Given_OsakaWithBlobDemandAtTheReservePriceBoundary_When_CalculatingExcessBlobGas_Then_ItDoesNotTrigger()
        {
            var reservePrice = new Eip7918ReservePriceInputs(
                parentBaseFeePerGas: new EvmUInt256(16UL),
                maxBlobsPerBlock: 9, targetBlobsPerBlock: 6,
                baseFeeUpdateFraction: new EvmUInt256(5_007_716UL));

            var result = BlobGasCalculator.CalculateExcessBlobGas(
                parentExcessBlobGas: 0, parentBlobGasUsed: 786432, targetBlobGasPerBlock: 786432, reservePrice);

            Assert.Equal(0UL, result);
        }

        [Fact]
        public void Given_OsakaWithBlobDemandBelowTheReservePrice_When_CalculatingExcessBlobGas_Then_TargetIsNotSubtracted()
        {
            var reservePrice = new Eip7918ReservePriceInputs(
                parentBaseFeePerGas: new EvmUInt256(17UL),
                maxBlobsPerBlock: 9, targetBlobsPerBlock: 6,
                baseFeeUpdateFraction: new EvmUInt256(5_007_716UL));

            var result = BlobGasCalculator.CalculateExcessBlobGas(
                parentExcessBlobGas: 0, parentBlobGasUsed: 917504, targetBlobGasPerBlock: 786432, reservePrice);

            Assert.Equal(305834UL, result);
        }

        [Fact]
        public void Given_ABpoForkWithItsOwnMaxAndTarget_When_TheReservePriceTriggers_Then_ItScalesByThatForksMaxAndTarget()
        {
            var reservePrice = new Eip7918ReservePriceInputs(
                parentBaseFeePerGas: new EvmUInt256(20UL),
                maxBlobsPerBlock: 15, targetBlobsPerBlock: 10,
                baseFeeUpdateFraction: new EvmUInt256(8_346_193UL));

            var result = BlobGasCalculator.CalculateExcessBlobGas(
                parentExcessBlobGas: 0, parentBlobGasUsed: 1441792, targetBlobGasPerBlock: 1310720, reservePrice);

            Assert.Equal(480597UL, result);
        }
    }
}
