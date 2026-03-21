using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.PrivacyPools
{
    public class SyncResult
    {
        public List<PoolAccount> PoolAccounts { get; set; } = new List<PoolAccount>();
        public PoseidonMerkleTree StateTree { get; set; }
        public ASPTreeService ASPTree { get; set; }
        public List<PoolDepositEventData> Deposits { get; set; } = new List<PoolDepositEventData>();
        public BigInteger LastBlockProcessed { get; set; }
    }
}
