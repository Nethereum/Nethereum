namespace Nethereum.EVM.Gas.Intrinsic
{
    /// <summary>
    /// EIP-7623 (Prague) calldata gas floor:
    /// <c>21000 + 10 × (zeroBytes + 4 × nonZeroBytes)</c>.
    /// </summary>
    public sealed class Eip7623CalldataFloorRule : ICalldataFloorRule
    {
        private const long G_TRANSACTION = 21000;
        private const long G_FLOOR_PER_TOKEN = 10;
        private const long G_TOKENS_PER_NONZERO = 4;

        public static readonly Eip7623CalldataFloorRule Instance = new Eip7623CalldataFloorRule();

        public long TokensInCalldata(byte[] data)
        {
            if (data == null || data.Length == 0) return 0;

            int zeroBytes = 0;
            int nonZeroBytes = 0;
            foreach (var b in data)
            {
                if (b == 0) zeroBytes++;
                else nonZeroBytes++;
            }
            return (long)zeroBytes + ((long)nonZeroBytes * G_TOKENS_PER_NONZERO);
        }

        public long CalculateFloor(byte[] data)
        {
            return G_TRANSACTION + (G_FLOOR_PER_TOKEN * TokensInCalldata(data));
        }
    }
}
