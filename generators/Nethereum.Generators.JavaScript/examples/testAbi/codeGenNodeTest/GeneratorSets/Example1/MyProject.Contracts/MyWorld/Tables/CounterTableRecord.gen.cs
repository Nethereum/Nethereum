using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Mud;
using Nethereum.Mud.Contracts.Core.Tables;
using Nethereum.Web3;
using System.Collections.Generic;
using System.Numerics;

namespace MyProject.Contracts.MyWorld.Tables
{
    public partial class CounterTableService : TableSingletonService<CounterTableRecord,CounterTableRecord.CounterValue>
    { 
        public CounterTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress) {}
    }
    
    public partial class CounterTableRecord : TableRecordSingleton<CounterTableRecord.CounterValue> 
    {
        public CounterTableRecord() : base("MyWorld", "Counter")
        {
        
        }

        public partial class CounterValue
        {
            [Parameter("uint32", "value", 1)]
            public virtual uint Value { get; set; }          
        }
    }
}
