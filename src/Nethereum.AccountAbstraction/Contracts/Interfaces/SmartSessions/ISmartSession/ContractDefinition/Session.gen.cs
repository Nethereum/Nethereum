using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.SmartSessions.ISmartSession.ContractDefinition
{
    public partial class Session : SessionBase { }

    public class SessionBase 
    {
        [Parameter("address", "sessionValidator", 1)]
        public virtual string SessionValidator { get; set; }
        [Parameter("bytes", "sessionValidatorInitData", 2)]
        public virtual byte[] SessionValidatorInitData { get; set; }
        [Parameter("bytes32", "salt", 3)]
        public virtual byte[] Salt { get; set; }
        [Parameter("tuple[]", "userOpPolicies", 4)]
        public virtual List<PolicyData> UserOpPolicies { get; set; }
        [Parameter("tuple", "erc7739Policies", 5)]
        public virtual ERC7739Data Erc7739Policies { get; set; }
        [Parameter("tuple[]", "actions", 6)]
        public virtual List<ActionData> Actions { get; set; }
        [Parameter("bool", "permitERC4337Paymaster", 7)]
        public virtual bool PermitERC4337Paymaster { get; set; }
    }
}
