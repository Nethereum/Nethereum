using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.UniActionPolicy.ContractDefinition
{
    public partial class ParamRule : ParamRuleBase { }

    public class ParamRuleBase 
    {
        [Parameter("uint8", "condition", 1)]
        public virtual byte Condition { get; set; }
        [Parameter("uint64", "offset", 2)]
        public virtual ulong Offset { get; set; }
        [Parameter("bool", "isLimited", 3)]
        public virtual bool IsLimited { get; set; }
        [Parameter("bytes32", "ref", 4)]
        public virtual byte[] Ref { get; set; }
        [Parameter("tuple", "usage", 5)]
        public virtual LimitUsage Usage { get; set; }
    }
}
