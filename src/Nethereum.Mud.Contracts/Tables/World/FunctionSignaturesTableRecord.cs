using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Mud.Contracts.Tables.World
{
    public class FunctionSignaturesTableRecord
    {
        public class FunctionSignaturesKey
        {
            [Parameter("bytes4", "functionSelector", 1)]
            public byte[] FunctionSelector { get; set; }
        }

        public class FunctionSignaturesValue
        {
            [Parameter("string", "functionSignature", 1)]
            public string FunctionSignature { get; set; }
        }
    }

}

