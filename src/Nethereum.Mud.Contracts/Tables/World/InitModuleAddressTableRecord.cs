using Nethereum.ABI.FunctionEncoding.Attributes;
using static Nethereum.Mud.Contracts.Tables.World.InitModuleAddressTableRecord;

namespace Nethereum.Mud.Contracts.Tables.World
{
    public class InitModuleAddressTableRecord : TableRecordSingleton<InitModuleAddressValue>
        {
            public InitModuleAddressTableRecord() : base("world", "InitModuleAddress")
            {
            }

            public class InitModuleAddressValue
            {
                [Parameter("address", "value", 1)]
                public string Value { get; set; }
            }
        }
    

}

