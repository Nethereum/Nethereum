namespace Nethereum.EVM.Gas.Intrinsic
{
    /// <summary>
    /// Per-fork rule for the EIP-3860 initcode word gas surcharge on
    /// contract-creation transactions. Installed on
    /// <see cref="IntrinsicGasRules"/> from Shanghai onwards; null on a
    /// bundle means "initcode word gas is not active at this fork"
    /// and the handler skips the surcharge entirely.
    /// </summary>
    public interface IInitCodeGasRule
    {
        long CalculateGas(byte[] initCode);
    }
}
