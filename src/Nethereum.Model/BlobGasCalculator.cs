using Nethereum.Util;

namespace Nethereum.Model
{
    public static class BlobGasCalculator
    {
        public const int GAS_PER_BLOB = 131072;
        public const int MIN_BASE_FEE_PER_BLOB_GAS = 1;
        public const int BLOB_BASE_FEE_UPDATE_FRACTION = 3338477;
        public const int MAX_BLOB_GAS_PER_BLOCK = 786432;
        public const int TARGET_BLOB_GAS_PER_BLOCK = 393216;

        public static EvmUInt256 CalculateBlobBaseFee(EvmUInt256 excessBlobGas)
        {
            return FakeExponential(
                new EvmUInt256(MIN_BASE_FEE_PER_BLOB_GAS),
                excessBlobGas,
                new EvmUInt256(BLOB_BASE_FEE_UPDATE_FRACTION));
        }

        public static EvmUInt256 CalculateBlobGasCost(int blobCount, EvmUInt256 blobBaseFee)
        {
            var blobGasUsed = new EvmUInt256((ulong)(blobCount * GAS_PER_BLOB));
            return blobGasUsed * blobBaseFee;
        }

        public static EvmUInt256 SuggestMaxFeePerBlobGas(EvmUInt256 excessBlobGas)
        {
            var baseFee = CalculateBlobBaseFee(excessBlobGas);
            if (baseFee.IsZero) baseFee = EvmUInt256.One;
            return baseFee * new EvmUInt256(2);
        }

        private static EvmUInt256 FakeExponential(EvmUInt256 factor, EvmUInt256 numerator, EvmUInt256 denominator)
        {
            var i = EvmUInt256.One;
            var output = EvmUInt256.Zero;
            var numeratorAccum = factor * denominator;
            while (!numeratorAccum.IsZero)
            {
                output = output + numeratorAccum;
                numeratorAccum = (numeratorAccum * numerator) / (denominator * i);
                i = i + EvmUInt256.One;
            }
            return output / denominator;
        }
    }
}
