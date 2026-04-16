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
        /// <summary>
        /// Returns the initcode word gas cost for a creation transaction
        /// whose initcode is <paramref name="initCode"/>. Implementations
        /// must return 0 for null or empty input.
        /// </summary>
        long CalculateGas(byte[] initCode);
    }
}
