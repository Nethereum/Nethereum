namespace Nethereum.EVM.Gas.Intrinsic
{
    public sealed class Eip7623CalldataFloorRule : ICalldataFloorRule
    {
        public static readonly Eip7623CalldataFloorRule Instance = new Eip7623CalldataFloorRule();

        public long FloorTokensInCalldata(byte[] data)
        {
            if (data == null || data.Length == 0) return 0;

            int zeroBytes = 0;
            int nonZeroBytes = 0;
            foreach (var b in data)
            {
                if (b == 0) zeroBytes++;
                else nonZeroBytes++;
            }
            return (long)zeroBytes + ((long)nonZeroBytes * GasConstants.TX_TOKENS_PER_NON_ZERO_BYTE);
        }

        public long FloorPerTokenGas(byte[] data)
        {
            return GasConstants.TX_FLOOR_PER_TOKEN * FloorTokensInCalldata(data);
        }
    }
}
