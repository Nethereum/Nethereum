using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;
using Nethereum.AccountAbstraction.Contracts.Rules.BadOutputRule.ContractDefinition;

namespace Nethereum.AccountAbstraction.Contracts.Rules.BadOutputRule.ContractDefinition
{


    public partial class BadOutputRuleDeployment : BadOutputRuleDeploymentBase
    {
        public BadOutputRuleDeployment() : base(BYTECODE) { }
        public BadOutputRuleDeployment(string byteCode) : base(byteCode) { }
    }

    public class BadOutputRuleDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60808060405234601557610111908161001a8239f35b5f80fdfe60806004361015600d575f80fd5b5f3560e01c631658af3714601f575f80fd5b3460d757602036600319011260d75760043567ffffffffffffffff811160d7573660238201121560d757806004013567ffffffffffffffff811160d7573691016024011160d75760208101600181526002604083015260408252606082019180831067ffffffffffffffff84111760c35760a0908360405260208452805180938160808401528383015e5f828483010152605f1992601f8019910116810103010190f35b634e487b7160e01b5f52604160045260245ffd5b5f80fdfea26469706673582212203cef6bf96f4cf147ca86fdbb74abd17636ed81ae32f4e876e07f30afb5058dcc64736f6c634300081c0033";
        public BadOutputRuleDeploymentBase() : base(BYTECODE) { }
        public BadOutputRuleDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class EvaluateFunction : EvaluateFunctionBase { }

    [Function("evaluate", "bytes")]
    public class EvaluateFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class EvaluateOutputDTO : EvaluateOutputDTOBase { }

    [FunctionOutput]
    public class EvaluateOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes", "output", 1)]
        public virtual byte[] Output { get; set; }
    }
}
