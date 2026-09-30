using Nethereum.Util;

namespace Nethereum.Model
{
    /// <summary>EIP-7918: "The current block's <c>blobSchedule</c> is used during processing."</summary>
    public readonly struct Eip7918ReservePriceInputs
    {
        public Eip7918ReservePriceInputs(
            EvmUInt256 parentBaseFeePerGas, int maxBlobsPerBlock, int targetBlobsPerBlock,
            EvmUInt256 baseFeeUpdateFraction)
        {
            ParentBaseFeePerGas = parentBaseFeePerGas;
            MaxBlobsPerBlock = maxBlobsPerBlock;
            TargetBlobsPerBlock = targetBlobsPerBlock;
            BaseFeeUpdateFraction = baseFeeUpdateFraction;
        }

        public EvmUInt256 ParentBaseFeePerGas { get; }
        public int MaxBlobsPerBlock { get; }
        public int TargetBlobsPerBlock { get; }
        public EvmUInt256 BaseFeeUpdateFraction { get; }
    }

    public static class BlobGasCalculator
    {
        public const int GAS_PER_BLOB = 131072;
        public const int MIN_BASE_FEE_PER_BLOB_GAS = 1;
        public const int BLOB_BASE_FEE_UPDATE_FRACTION = 3338477;
        public const int MAX_BLOB_GAS_PER_BLOCK = 786432;
        public const int TARGET_BLOB_GAS_PER_BLOCK = 393216;

        /// <summary>EIP-7918 §Parameters: <c>BLOB_BASE_COST</c> = <c>2**13</c>.</summary>
        public const int BLOB_BASE_COST = 8192;

        /// <summary>
        /// EIP-4844 <c>get_total_blob_gas(tx) = GAS_PER_BLOB * len(tx.blob_versioned_hashes)</c>.
        /// The spec scalar is unbounded, so the product is formed at 64-bit width.
        /// </summary>
        public static ulong CalculateTotalBlobGas(int blobCount)
        {
            return (ulong)blobCount * (ulong)GAS_PER_BLOB;
        }

        public static EvmUInt256 CalculateBlobBaseFee(EvmUInt256 excessBlobGas)
        {
            return CalculateBlobBaseFee(excessBlobGas, new EvmUInt256(BLOB_BASE_FEE_UPDATE_FRACTION));
        }

        /// <summary>
        /// EIP-7918 §Specification:
        /// <c>def calc_excess_blob_gas(parent: Header) -> int:
        ///     target_blob_gas = GAS_PER_BLOB * blobSchedule.target
        ///     if parent.excess_blob_gas + parent.blob_gas_used &lt; target_blob_gas: return 0
        ///     if BLOB_BASE_COST * parent.base_fee_per_gas &gt; GAS_PER_BLOB * get_base_fee_per_blob_gas(parent):
        ///         return parent.excess_blob_gas + parent.blob_gas_used * (blobSchedule.max - blobSchedule.target) // blobSchedule.max
        ///     else:
        ///         return parent.excess_blob_gas + parent.blob_gas_used - target_blob_gas</c>
        /// </summary>
        public static ulong CalculateExcessBlobGas(
            ulong parentExcessBlobGas, ulong parentBlobGasUsed, ulong targetBlobGasPerBlock,
            Eip7918ReservePriceInputs? reservePrice = null)
        {
            ulong sum = parentExcessBlobGas + parentBlobGasUsed;
            if (sum < targetBlobGasPerBlock) return 0UL;

            if (reservePrice is Eip7918ReservePriceInputs r)
            {
                var blobPriceAtParent = CalculateBlobBaseFee(
                    new EvmUInt256(parentExcessBlobGas), r.BaseFeeUpdateFraction) * new EvmUInt256((ulong)GAS_PER_BLOB);
                var reserveBlobPrice = new EvmUInt256((ulong)BLOB_BASE_COST) * r.ParentBaseFeePerGas;

                if (reserveBlobPrice > blobPriceAtParent)
                {
                    var scaledExcess = parentBlobGasUsed * (ulong)(r.MaxBlobsPerBlock - r.TargetBlobsPerBlock)
                        / (ulong)r.MaxBlobsPerBlock;
                    return parentExcessBlobGas + scaledExcess;
                }
            }

            return sum - targetBlobGasPerBlock;
        }

        /// <summary>
        /// EIP-4844 <c>get_base_fee_per_blob_gas</c> against a caller-supplied
        /// <c>BLOB_BASE_FEE_UPDATE_FRACTION</c>, which is the only part of the
        /// formula a fork changes.
        /// </summary>
        public static EvmUInt256 CalculateBlobBaseFee(EvmUInt256 excessBlobGas, EvmUInt256 baseFeeUpdateFraction)
        {
            return FakeExponential(
                new EvmUInt256(MIN_BASE_FEE_PER_BLOB_GAS),
                excessBlobGas,
                baseFeeUpdateFraction);
        }

        /// <summary>
        /// EIP-4844 <c>calc_blob_fee(header, tx) = get_total_blob_gas(tx) * get_base_fee_per_blob_gas(header)</c>.
        /// </summary>
        public static EvmUInt256 CalculateBlobGasCost(int blobCount, EvmUInt256 blobBaseFee)
        {
            var totalBlobGas = CalculateTotalBlobGas(blobCount);
            return new EvmUInt256(totalBlobGas) * blobBaseFee;
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
