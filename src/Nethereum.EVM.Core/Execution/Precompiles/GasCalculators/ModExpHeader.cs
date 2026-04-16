using Nethereum.Util;

namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    /// <summary>
    /// Parsed MODEXP (0x05) precompile header: the three uint256 length
    /// fields plus the top 32 bytes of the exponent (used by the Berlin
    /// and Osaka gas formulas to extract iteration count). Produced by
    /// <see cref="ModExpHeaderParser.Parse(byte[])"/> and consumed by
    /// <see cref="Eip2565ModExpGasCalculator"/> /
    /// <see cref="Eip7883ModExpGasCalculator"/>.
    /// </summary>
    public readonly struct ModExpHeader
    {
        public readonly EvmUInt256 BaseLen;
        public readonly EvmUInt256 ExpLen;
        public readonly EvmUInt256 ModLen;
        public readonly EvmUInt256 ExpHead;
        public readonly int ExpBitLen;

        public ModExpHeader(
            EvmUInt256 baseLen,
            EvmUInt256 expLen,
            EvmUInt256 modLen,
            EvmUInt256 expHead,
            int expBitLen)
        {
            BaseLen = baseLen;
            ExpLen = expLen;
            ModLen = modLen;
            ExpHead = expHead;
            ExpBitLen = expBitLen;
        }
    }
}
