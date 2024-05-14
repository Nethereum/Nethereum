using Nethereum.ABI.FunctionEncoding.Attributes;
using static Nethereum.Mud.Contracts.World.Tables.FunctionSelectorsTableRecord;
using Nethereum.Mud.EncodingDecoding;

namespace Nethereum.Mud.Contracts.World.Tables
{
    public class FunctionSelectorsTableRecord : TableRecord<FunctionSelectorsKey, FunctionSelectorsValue>
    {
        public FunctionSelectorsTableRecord() : base("world", "FunctionSelectors")
        {
        }

        public class FunctionSelectorsKey
        {
            [Parameter("bytes4", "worldFunctionSelector", 1)]
            public byte[] WorldFunctionSelector { get; set; }
        }

        public class FunctionSelectorsValue
        {
            [Parameter("bytes32", "systemId", 1)]
            public byte[] SystemId { get; set; }
            [Parameter("bytes4", "systemFunctionSelector", 2)]
            public byte[] SystemFunctionSelector { get; set; }

            public Resource GetSystemIdResource()
            {
                return ResourceEncoder.Decode(SystemId);
            }
        }
    }

}

