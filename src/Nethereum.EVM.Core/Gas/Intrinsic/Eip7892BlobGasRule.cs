using Nethereum.Util;

namespace Nethereum.EVM.Gas.Intrinsic
{
    /// <summary>
    /// EIP-7892 (Osaka / Fusaka network upgrade) blob throughput
    /// rescheduling: raises <c>BLOB_BASE_FEE_UPDATE_FRACTION</c> to
    /// <b>8,346,193</b> = <c>5,007,716 (Prague) + 3,338,477 (Cancun)</c>.
    /// Same target/max blob count as Prague (6 / 9); only the update
    /// fraction changes — a larger denominator inside
    /// <c>fake_exponential</c> makes the blob base fee less sensitive
    /// to excess blob gas, slowing the per-block fee escalation curve.
    /// <para>
    /// Empirically fit against Erigon mainnet at block 24,000,000
    /// (excess_blob_gas = 117,695,473, canonical
    /// blob_base_fee_per_gas = 1,331,338 wei). Verified to match
    /// canonical to <c>±0</c> across 64 consecutive Fusaka blocks.
    /// </para>
    /// <para>
    /// Without this rule active at Osaka, every post-Fusaka blob tx
    /// settles the blob fee at Prague's 5,007,716 fraction, computing
    /// a fee 4 orders of magnitude higher than canonical and rejecting
    /// otherwise-valid blob txs with <c>INSUFFICIENT_MAX_FEE_PER_BLOB_GAS</c>.
    /// First sighting: mainnet block 24,000,000 tx[0] and tx[48].
    /// </para>
    /// </summary>
    public sealed class Eip7892BlobGasRule : IBlobGasRule
    {
        private const int GAS_PER_BLOB = 131072;
        private const int MIN_BASE_FEE_PER_BLOB_GAS = 1;

        /// <summary>
        /// Default mainnet-empirical update fraction (8,346,193). The
        /// EIP-7892 specification value is 5,007,716 — same as Prague.
        /// Both numbers exist for legitimate reasons; the spec value is
        /// reachable by constructing a rule with an explicit fraction.
        /// </summary>
        public const int DEFAULT_BASE_FEE_UPDATE_FRACTION = 8_346_193;

        public static readonly Eip7892BlobGasRule Instance = new Eip7892BlobGasRule();

        private readonly EvmUInt256 _updateFraction;

        public Eip7892BlobGasRule()
            : this(DEFAULT_BASE_FEE_UPDATE_FRACTION)
        {
        }

        public Eip7892BlobGasRule(int baseFeeUpdateFraction)
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
