using Nethereum.Util;

namespace Nethereum.EVM.Gas.Intrinsic
{
    /// <summary>
    /// Per-fork rule for EIP-4844 blob gas. Installed on
    /// <see cref="IntrinsicGasRules"/> from Cancun onwards; null on a
    /// bundle means "blob gas is not active at this fork" and type-3
    /// (blob) transactions are rejected earlier by tx-type validation.
    /// The <see cref="TransactionExecutor"/> reads the nullability of
    /// this slot directly instead of consulting a fork-enable flag.
    /// </summary>
    public interface IBlobGasRule
    {
        EvmUInt256 CalculateBlobBaseFee(EvmUInt256 excessBlobGas);

        EvmUInt256 CalculateBlobGasCost(int blobCount, EvmUInt256 blobBaseFee);

        EvmUInt256 BaseFeeUpdateFraction { get; }

        bool AppliesReservePrice { get; }
    }
}
