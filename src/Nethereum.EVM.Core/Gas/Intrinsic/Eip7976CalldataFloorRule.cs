namespace Nethereum.EVM.Gas.Intrinsic
{
    /// <summary>
    /// EIP-7976 §Specification: "Equivalently, the floor cost is <c>64</c> gas per calldata
    /// byte (both zero and non-zero), since <c>TOTAL_COST_FLOOR_PER_TOKEN * 4 = 64</c>."
    /// </summary>
    public sealed class Eip7976CalldataFloorRule : ICalldataFloorRule
    {
        public static readonly Eip7976CalldataFloorRule Instance = new Eip7976CalldataFloorRule();

        public long FloorTokensInCalldata(byte[] data)
        {
            if (data == null || data.Length == 0) return 0;

            return (long)data.Length * GasConstants.EIP7976_FLOOR_TOKENS_PER_BYTE;
        }

        public long FloorPerTokenGas(byte[] data)
        {
            return GasConstants.EIP7976_FLOOR_PER_TOKEN_GAS * FloorTokensInCalldata(data);
        }
    }
}
