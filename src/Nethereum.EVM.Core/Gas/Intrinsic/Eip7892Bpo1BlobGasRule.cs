using Nethereum.Util;

namespace Nethereum.EVM.Gas.Intrinsic
{
    /// <summary>
    /// First BPO (Blob Parameter Only) fork after Osaka activation, per
    /// EIP-7892. Raises <c>BLOB_BASE_FEE_UPDATE_FRACTION</c> from Osaka's
    /// <b>8,346,193</b> to <b>11,684,671</b>. Same target/max blob count
    /// as Prague/Osaka (6 / 9); only the rescheduling fraction changes.
    /// <para>
    /// Empirically fit against Erigon mainnet at block 24,179,383
    /// (the first BPO1 block — timestamp 1,767,747,671,
    /// 2026-01-07 01:01:11 UTC) and 127 subsequent blocks. The constant
    /// 11,684,671 matches canonical <c>baseFeePerBlobGas</c> to absolute
    /// error 0 on every sample.
    /// </para>
    /// <para>
    /// Without this rule active at BPO1 timestamp, mainnet replays of
    /// blocks past the BPO1 boundary compute blob_base_fee 2–3 orders
    /// of magnitude higher than canonical, rejecting otherwise-valid
    /// blob txs with <c>INSUFFICIENT_MAX_FEE_PER_BLOB_GAS</c>.
    /// </para>
    /// </summary>
    public sealed class Eip7892Bpo1BlobGasRule : IBlobGasRule
    {
        private const int GAS_PER_BLOB = 131072;
        private const int MIN_BASE_FEE_PER_BLOB_GAS = 1;
        private const int BLOB_BASE_FEE_UPDATE_FRACTION = 11_684_671;

        public static readonly Eip7892Bpo1BlobGasRule Instance = new Eip7892Bpo1BlobGasRule();

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
