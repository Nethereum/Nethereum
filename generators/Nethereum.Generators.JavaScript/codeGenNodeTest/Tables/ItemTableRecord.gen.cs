using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Mud;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Mud.Contracts.Core.Tables;
using Nethereum.Web3;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Unity.Contracts.Standards.Tables
{
    public partial class ItemTableService : TableService<ItemTableRecord, ItemTableRecord.ItemKey, ItemTableRecord.ItemValue>
    { 
        public ItemTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress) {}
        public virtual Task<ItemTableRecord> GetTableRecordAsync(uint id, BlockParameter blockParameter = null)
        {
            var _key = new ItemTableRecord.ItemKey();
            _key.Id = id;
            return GetTableRecordAsync(_key, blockParameter);
        }
        public virtual Task<string> SetRecordRequestAsync(uint id, uint price, string name, string description, string owner)
        {
            var _key = new ItemTableRecord.ItemKey();
            _key.Id = id;

            var _values = new ItemTableRecord.ItemValue();
            _values.Price = price;
            _values.Name = name;
            _values.Description = description;
            _values.Owner = owner;
            return SetRecordRequestAsync(_key, _values);
        }
        public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync(uint id, uint price, string name, string description, string owner)
        {
            var _key = new ItemTableRecord.ItemKey();
            _key.Id = id;

            var _values = new ItemTableRecord.ItemValue();
            _values.Price = price;
            _values.Name = name;
            _values.Description = description;
            _values.Owner = owner;
            return SetRecordRequestAndWaitForReceiptAsync(_key, _values);
        }
    }
    
    public partial class ItemTableRecord : TableRecord<ItemTableRecord.ItemKey, ItemTableRecord.ItemValue> 
    {
        public ItemTableRecord() : base("Item")
        {
        
        }
        /// <summary>
        /// Direct access to the key property 'Id'.
        /// </summary>
        public virtual uint Id => Keys.Id;
        /// <summary>
        /// Direct access to the value property 'Price'.
        /// </summary>
        public virtual uint Price => Values.Price;
        /// <summary>
        /// Direct access to the value property 'Name'.
        /// </summary>
        public virtual string Name => Values.Name;
        /// <summary>
        /// Direct access to the value property 'Description'.
        /// </summary>
        public virtual string Description => Values.Description;
        /// <summary>
        /// Direct access to the value property 'Owner'.
        /// </summary>
        public virtual string Owner => Values.Owner;

        public partial class ItemKey
        {
            [Parameter("uint32", "id", 1)]
            public virtual uint Id { get; set; }
        }

        public partial class ItemValue
        {
            [Parameter("uint32", "price", 1)]
            public virtual uint Price { get; set; }
            [Parameter("string", "name", 2)]
            public virtual string Name { get; set; }
            [Parameter("string", "description", 3)]
            public virtual string Description { get; set; }
            [Parameter("string", "owner", 4)]
            public virtual string Owner { get; set; }          
        }
    }
}
