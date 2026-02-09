using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.UniActionPolicy.ContractDefinition
{
    public partial class LimitUsage : LimitUsageBase { }

    public class LimitUsageBase 
    {
        [Parameter("uint256", "limit", 1)]
        public virtual BigInteger Limit { get; set; }
        [Parameter("uint256", "used", 2)]
        public virtual BigInteger Used { get; set; }
    }
}
