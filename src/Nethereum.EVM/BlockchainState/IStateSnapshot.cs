using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.EVM.BlockchainState
{
    public interface IStateSnapshot
    {
        int SnapshotId { get; }
        Dictionary<string, AccountStateSnapshot> AccountSnapshots { get; }
        HashSet<string> WarmAddresses { get; }
    }

    public class AccountStateSnapshot
    {
        public string Address { get; set; }
        public Dictionary<BigInteger, byte[]> Storage { get; set; }
        public BigInteger? ExecutionBalance { get; set; }
        public BigInteger? Nonce { get; set; }
        public byte[] Code { get; set; }
        public HashSet<BigInteger> WarmStorageKeys { get; set; }
    }
}
