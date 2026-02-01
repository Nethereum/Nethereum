using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SocialRecoveryModule.ContractDefinition
{
    public partial class RecoveryRequest : RecoveryRequestBase { }

    public class RecoveryRequestBase 
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner { get; set; }
        [Parameter("uint64", "executeAfter", 2)]
        public virtual ulong ExecuteAfter { get; set; }
        [Parameter("uint32", "approvalCount", 3)]
        public virtual uint ApprovalCount { get; set; }
        [Parameter("uint8", "status", 4)]
        public virtual byte Status { get; set; }
    }
}
