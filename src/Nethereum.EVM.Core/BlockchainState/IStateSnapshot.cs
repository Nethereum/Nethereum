using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.EVM.BlockchainState
{
    public interface IStateSnapshot
    {
        int SnapshotId { get; }
        Dictionary<string, AccountStateSnapshot> AccountSnapshots { get; }
        HashSet<string> WarmAddresses { get; }
        HashSet<string> SelfDestructedAddresses { get; }
        Dictionary<string, Dictionary<EvmUInt256, byte[]>> TransientStorage { get; }
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
        /// <summary>
        /// EIP-161 dirty-bit captured at snapshot time. Restored on
        /// revert so reverted sub-calls roll back their touches
        /// (matches popping the journalled dirty set on revert).
        /// </summary>
        public bool IsTouched { get; set; }
    }
}
