using Nethereum.Util;

namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
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
