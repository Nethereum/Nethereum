using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Mud.Contracts.BatchCallSystem.ContractDefinition
{
    public partial class SystemCallData : SystemCallDataBase { }

    public class SystemCallDataBase 
    {
        [Parameter("bytes32", "systemId", 1)]
        public virtual byte[] SystemId { get; set; }
        [Parameter("bytes", "callData", 2)]
        public virtual byte[] CallData { get; set; }
    }
}
