using Nethereum.Util;

namespace Nethereum.EVM.Gas.Intrinsic
{
    /// <summary>
    /// EIP-7691 (Prague) blob throughput increase: bumps the blob target
    /// from 3 → 6 and max from 6 → 9 blobs per block, and — most
    /// importantly for EVM correctness — raises the BLOB_BASE_FEE update
    /// fraction from 3,338,477 (Cancun) → 5,007,716. The update fraction
    /// is the denominator inside <c>fake_exponential</c>, so a larger
    /// fraction makes the blob base fee less sensitive to excess blob gas
    /// (slower growth on the same load). Without this rule active at
    /// Prague, every Prague blob tx settles the blob fee at the Cancun
    /// rate, producing per-sender balance drift vs canonical (first
    /// sighting: mainnet block 22,000,000 tx-level acct diffs).
    /// </summary>
    public sealed class Eip7691BlobGasRule : IBlobGasRule
    {
        private const int GAS_PER_BLOB = 131072;
        private const int MIN_BASE_FEE_PER_BLOB_GAS = 1;
        public const int DEFAULT_BASE_FEE_UPDATE_FRACTION = 5_007_716;

        public static readonly Eip7691BlobGasRule Instance = new Eip7691BlobGasRule();

        private readonly EvmUInt256 _updateFraction;

        public Eip7691BlobGasRule()
            : this(DEFAULT_BASE_FEE_UPDATE_FRACTION)
        {
        }

        public Eip7691BlobGasRule(int baseFeeUpdateFraction)
        {
            _updateFraction = new EvmUInt256(baseFeeUpdateFraction);
        }

        public EvmUInt256 CalculateBlobBaseFee(EvmUInt256 excessBlobGas)
        {
            return FakeExponential(
                new EvmUInt256(MIN_BASE_FEE_PER_BLOB_GAS),
                excessBlobGas,
                _updateFraction);
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
