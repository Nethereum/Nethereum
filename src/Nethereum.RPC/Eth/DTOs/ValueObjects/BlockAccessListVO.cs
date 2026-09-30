using System.Collections.Generic;

namespace Nethereum.RPC.Eth.DTOs
{
    public class BlockAccessListVO
    {
        public BlockAccessListVO()
        {
        }

        public BlockAccessListVO(long blockNumber, string blockHash, List<AccountAccess> accounts)
        {
            BlockNumber = blockNumber;
            BlockHash = blockHash;
            Accounts = accounts;
        }

        public long BlockNumber { get; private set; }
        public string BlockHash { get; private set; }
        public List<AccountAccess> Accounts { get; private set; }
    }
}
