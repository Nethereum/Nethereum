using Nethereum.Mud.Contracts.Core.Tables;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;

namespace Nethereum.Mud.IntegrationTests.MudTest.Tables
{
    //public partial class ItemTableService : TableService<ItemTableRecord, ItemTableRecord.ItemKey, ItemTableRecord.ItemValue>
    //{
       
    //    public ItemTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
    //    {
    //    }

    //    public virtual Task<ItemTableRecord> GetTableRecordAsync(int id, BlockParameter blockParameter = null)
    //    {
    //        var key = new ItemTableRecord.ItemKey();
    //        key.Id = id;
    //        return GetTableRecordAsync(key, blockParameter);
    //    }

    //    public virtual Task<string> SetRecordRequestAsync(int id, int price, string name, string description, string owner)
    //    {
    //        var key = new ItemTableRecord.ItemKey();
    //        key.Id = id;
    //        var values = new ItemTableRecord.ItemValue();
    //        values.Price = price;
    //        values.Name = name;
    //        values.Description = description;
    //        values.Owner = owner;
    //        return SetRecordRequestAsync(key, values);
    //    }

    //    public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceipt(int id, int price, string name, string description, string owner)
    //    {
    //        var key = new ItemTableRecord.ItemKey();
    //        key.Id = id;
    //        var values = new ItemTableRecord.ItemValue();
    //        values.Price = price;
    //        values.Name = name;
    //        values.Description = description;
    //        values.Owner = owner;
    //        return SetRecordRequestAndWaitForReceiptAsync(key, values);
    //    }
    //}

}
