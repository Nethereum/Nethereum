using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.UniActionPolicy.ContractDefinition
{
    public partial class ParamRules : ParamRulesBase { }

    public class ParamRulesBase 
    {
        [Parameter("uint256", "length", 1)]
        public virtual BigInteger Length { get; set; }
        [Parameter("tuple[16]", "rules", 2)]
        public virtual List<ParamRule> Rules { get; set; }
    }
}
