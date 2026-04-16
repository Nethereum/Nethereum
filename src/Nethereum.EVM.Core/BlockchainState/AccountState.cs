using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.EVM.BlockchainState
{
    public class AccountState
    {
        public EvmUInt256 Balance { get; set; }
        public EvmUInt256 Nonce { get; set; }
        public byte[] Code { get; set; } = new byte[0];
        public Dictionary<EvmUInt256, byte[]> Storage { get; set; } = new Dictionary<EvmUInt256, byte[]>();
    }
}
