using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Mud.Contracts.BatchCallSystem.ContractDefinition
{
    public partial class SystemCallFromData : SystemCallFromDataBase { }

    public class SystemCallFromDataBase 
    {
        [Parameter("address", "from", 1)]
        public virtual string From { get; set; }
        [Parameter("bytes32", "systemId", 2)]
        public virtual byte[] SystemId { get; set; }
        [Parameter("bytes", "callData", 3)]
        public virtual byte[] CallData { get; set; }
    }
}
