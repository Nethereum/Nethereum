using Nethereum.Util;

namespace Nethereum.EVM.Types
{
    public class EvmLog
    {
        public string Address { get; set; }
        public string[] Topics { get; set; }
        public string Data { get; set; }
        public EvmUInt256 LogIndex { get; set; }
    }
}
