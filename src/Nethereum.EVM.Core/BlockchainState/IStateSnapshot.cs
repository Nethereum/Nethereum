using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.EVM.BlockchainState
{
    public interface IStateSnapshot
    {
        int SnapshotId { get; }
        Dictionary<EvmAddress, AccountStateSnapshot> AccountSnapshots { get; }
        HashSet<EvmAddress> WarmAddresses { get; }
        HashSet<EvmAddress> SelfDestructedAddresses { get; }
        Dictionary<EvmAddress, Dictionary<EvmUInt256, byte[]>> TransientStorage { get; }
    }

    public class AccountStateSnapshot
    {
        public string Address { get; set; }
        public Dictionary<EvmUInt256, byte[]> Storage { get; set; }
        public EvmUInt256? ExecutionBalance { get; set; }
        public EvmUInt256? InitialChainBalance { get; set; }
        public EvmUInt256? Nonce { get; set; }
        public byte[] Code { get; set; }
        public HashSet<EvmUInt256> WarmStorageKeys { get; set; }
        public bool IsNewContract { get; set; }
        public bool IsTouched { get; set; }
        public bool IsRemoved { get; set; }
    }
}
