using System.Numerics;
using Nethereum.BlockchainProcessing.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.Core.Tables;
using static Nethereum.Mud.Contracts.World.Tables.BalancesTableRecord;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.World;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Mud.Contracts.World.Tables
{
    public partial class BalancesTableService : TableService<BalancesTableRecord, BalancesKey, BalancesValue>
    {
      
        public BalancesTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<BalancesTableRecord> GetTableRecordAsync(byte[] namespaceResourceId, BlockParameter blockParameter = null)
        {
            var key = new BalancesKey();
            key.NamespaceId = namespaceResourceId;
            return GetTableRecordAsync(key, blockParameter);
        }

        public virtual Task<string> SetRecordRequestAsync(byte[] namespaceResourceId, BigInteger balance)
        {
            var key = new BalancesKey();
            key.NamespaceId = namespaceResourceId;
            var values = new BalancesValue();
            values.Balance = balance;
            return SetRecordRequestAsync(key, values);
        }

        public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceipt(byte[] namespaceResourceId, BigInteger balance)
        {
            var key = new BalancesKey();
            key.NamespaceId = namespaceResourceId;
            var values = new BalancesValue();
            values.Balance = balance;
            return SetRecordRequestAndWaitForReceiptAsync(key, values);
        }
    }
}
