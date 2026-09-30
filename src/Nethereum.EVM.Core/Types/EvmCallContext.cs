using Nethereum.Util;

namespace Nethereum.EVM.Types
{
    public class EvmCallContext
    {
        public string From { get; set; }
        public string To { get; set; }
        public byte[] Data { get; set; }
        public EvmUInt256 Value { get; set; }
        public long Gas { get; set; }
        public EvmUInt256 GasPrice { get; set; }
        public EvmUInt256 ChainId { get; set; }
        public EvmUInt256 Nonce { get; set; }
        public EvmUInt256 MaxFeePerGas { get; set; }
        public EvmUInt256 MaxPriorityFeePerGas { get; set; }
    }
}
