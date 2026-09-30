namespace Nethereum.EVM.Gas.Intrinsic
{
    /// <summary>
    /// Per-fork rule for the EIP-2780 recipient/value component of the
    /// intrinsic gas base, added on top of <see cref="IntrinsicGasRules.TxBase"/>.
    /// Installed on <see cref="IntrinsicGasRules"/> from Amsterdam onwards; null
    /// on a bundle means "the base is not decomposed at this fork" and the
    /// caller falls back to the flat <see cref="IntrinsicGasRules.TxCreate"/>
    /// adder for contract creation and no adder at all for a call.
    /// </summary>
    public interface IRecipientGasRule
    {
        long CalculateRecipientGas(bool isContractCreation, bool isSelfTransfer, bool hasValue);
    }
}
