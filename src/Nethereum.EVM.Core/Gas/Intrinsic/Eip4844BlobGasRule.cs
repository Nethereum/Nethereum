using Nethereum.Util;

namespace Nethereum.EVM.Gas.Intrinsic
{
    /// <summary>
    /// EIP-4844 (Cancun) blob gas rule: computes the blob base fee from
    /// the block's excess blob gas counter via the
    /// <c>fake_exponential</c> formula, and derives the per-transaction
    /// blob gas cost as <c>blobCount × GAS_PER_BLOB × blobBaseFee</c>.
    /// All math runs on <see cref="EvmUInt256"/>.
    /// </summary>
    public sealed class Eip4844BlobGasRule : IBlobGasRule
    {
        private const int GAS_PER_BLOB = 131072;                 // 2^17
        private const int MIN_BASE_FEE_PER_BLOB_GAS = 1;
        private const int BLOB_BASE_FEE_UPDATE_FRACTION = 3338477;

        public static readonly Eip4844BlobGasRule Instance = new Eip4844BlobGasRule();

        public EvmUInt256 CalculateBlobBaseFee(EvmUInt256 excessBlobGas)
        {
            return FakeExponential(
                new EvmUInt256(MIN_BASE_FEE_PER_BLOB_GAS),
                excessBlobGas,
                new EvmUInt256(BLOB_BASE_FEE_UPDATE_FRACTION));
        }

        public EvmUInt256 CalculateBlobGasCost(int blobCount, EvmUInt256 blobBaseFee)
        {
            var blobGasUsed = new EvmUInt256((ulong)(blobCount * GAS_PER_BLOB));
            return blobGasUsed * blobBaseFee;
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
