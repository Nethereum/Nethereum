using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition
{
    public partial class ActionData : ActionDataBase { }

    public class ActionDataBase 
    {
        [Parameter("bytes4", "actionTargetSelector", 1)]
        public virtual byte[] ActionTargetSelector { get; set; }
        [Parameter("address", "actionTarget", 2)]
        public virtual string ActionTarget { get; set; }
        [Parameter("tuple[]", "actionPolicies", 3)]
        public virtual List<PolicyData> ActionPolicies { get; set; }
    }
}
