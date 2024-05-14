using Nethereum.ABI.FunctionEncoding.Attributes;
using static Nethereum.Mud.Contracts.World.Tables.InitModuleAddressTableRecord;

namespace Nethereum.Mud.Contracts.World.Tables
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

