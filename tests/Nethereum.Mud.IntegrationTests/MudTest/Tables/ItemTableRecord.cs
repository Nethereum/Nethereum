using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Nethereum.Mud.IntegrationTests.MudTest.Tables.ItemTableRecord;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Mud.IntegrationTests.MudTest.Tables
{
    public class ItemTableRecord : TableRecord<ItemKey, ItemValue>
    {
        public ItemTableRecord() : base("Item")
        {

        }

        public class ItemKey
        {
            [Parameter("uint32", "id", 1)]
            public int Id { get; set; }
        }

        public class ItemValue
        {
            [Parameter("uint32", "price", 1)]
            public int Price { get; set; }
            [Parameter("string", "name", 2)]
            public string Name { get; set; }
            [Parameter("string", "description", 3)]
            public string Description { get; set; }
            [Parameter("string", "owner", 4)]
            public string Owner { get; set; }
        }
    }
}
