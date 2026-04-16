namespace Nethereum.EVM.Gas.Intrinsic
{
    /// <summary>
    /// Per-fork rule for the EIP-7623 calldata gas floor. Installed on
    /// <see cref="IntrinsicGasRules"/> from Prague onwards; null on a
    /// bundle means "the calldata floor does not apply at this fork"
    /// and <see cref="IntrinsicGasRules.CalculateFloorGasLimit"/>
    /// returns 0, making the floor check a no-op at the call site.
    /// </summary>
    public interface ICalldataFloorRule
    {
        /// <summary>
        /// Returns the EIP-7623 token count for <paramref name="data"/>:
        /// each zero byte counts as 1 token and each non-zero byte as
        /// <c>TOKENS_PER_NONZERO</c> tokens. Implementations return 0
        /// for null or empty input.
        /// </summary>
        long TokensInCalldata(byte[] data);

        /// <summary>
        /// Returns the raw calldata floor
        /// <c>G_TRANSACTION + G_FLOOR_PER_TOKEN × TokensInCalldata(data)</c>
        /// without the contract-creation adder (callers that need the
        /// validation-time floor add <c>G_TXCREATE</c> themselves via
        /// <see cref="IntrinsicGasRules.CalculateFloorGasLimit"/>).
        /// </summary>
        long CalculateFloor(byte[] data);
    }
}
