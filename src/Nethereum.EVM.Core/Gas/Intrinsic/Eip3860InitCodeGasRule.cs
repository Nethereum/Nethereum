namespace Nethereum.EVM.Gas.Intrinsic
{
    /// <summary>
    /// EIP-3860 (Shanghai) initcode word gas: 2 gas per 32-byte word of
    /// the contract-creation initcode.
    /// </summary>
    public sealed class Eip3860InitCodeGasRule : IInitCodeGasRule
    {
        private const long G_INITCODE_WORD = 2;

        public static readonly Eip3860InitCodeGasRule Instance = new Eip3860InitCodeGasRule();

        public long CalculateGas(byte[] initCode)
        {
            if (initCode == null || initCode.Length == 0) return 0;
            int words = (initCode.Length + 31) / 32;
            return (long)words * G_INITCODE_WORD;
        }
    }
}
