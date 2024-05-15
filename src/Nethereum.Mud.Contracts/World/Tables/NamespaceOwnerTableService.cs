using System.Numerics;
using Nethereum.BlockchainProcessing.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.Core.Tables;
using static Nethereum.Mud.Contracts.World.Tables.NamespaceOwnerTableRecord;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.World;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Mud.Contracts.World.Tables
{
    public partial class NamespaceOwnerTableService : TableService<NamespaceOwnerTableRecord, NamespaceOwnerKey, NamespaceOwnerValue>
    {
     

        public NamespaceOwnerTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<NamespaceOwnerTableRecord> GetTableRecordAsync(byte[] namespaceId, BlockParameter blockParameter = null)
        {
            var key = new NamespaceOwnerKey();
            key.NamespaceId = namespaceId;
            return GetTableRecordAsync(key, blockParameter);
        }

        public virtual Task<string> SetRecordRequestAsync(byte[] namespaceId, string owner)
        {
            var key = new NamespaceOwnerKey();
            key.NamespaceId = namespaceId;
            var values = new NamespaceOwnerValue();
            values.Owner = owner;

            return SetRecordRequestAsync(key, values);
        }

        public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceipt(byte[] namespaceId, string owner)
        {
            var key = new NamespaceOwnerKey();
            key.NamespaceId = namespaceId;
            var values = new NamespaceOwnerValue();
            values.Owner = owner;
            return SetRecordRequestAndWaitForReceiptAsync(key, values);
        }

    }
}
