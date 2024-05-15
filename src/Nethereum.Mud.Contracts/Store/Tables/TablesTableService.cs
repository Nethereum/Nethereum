using static Nethereum.Mud.Contracts.Store.Tables.TablesTableRecord;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;
using Nethereum.Mud.Contracts.World;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Mud.Contracts.Core.Tables;
using Nethereum.Mud.EncodingDecoding;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Web3;
using Nethereum.Mud.Contracts.Core.StoreEvents;

namespace Nethereum.Mud.Contracts.Store.Tables
{
  
    public partial class TablesTableService : TableService<TablesTableRecord, TablesKey, TablesValue>
    {
      

        public TablesTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {

        }

        public virtual Task<TablesTableRecord> GetTableRecordAsync(byte[] tableId, BlockParameter blockParameter = null)
        {
            var key = new TablesKey();
            key.TableId = tableId;
            return GetTableRecordAsync(key, blockParameter);
        }

        public virtual Task<string> SetRecordRequestAsync(byte[] tableId, SchemaEncoded schemaEncoded)
        {
            var key = new TablesKey();
            key.TableId = tableId;
            var values = new TablesValue();
            values.SetValuesFromSchema(schemaEncoded);
            return SetRecordRequestAsync(key, values);
        }

        public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceipt(byte[] tableId, SchemaEncoded schemaEncoded)
        {
            var key = new TablesKey();
            key.TableId = tableId;
            var values = new TablesValue();
            values.SetValuesFromSchema(schemaEncoded);
            return SetRecordRequestAndWaitForReceiptAsync(key, values);
        }
    }
}

