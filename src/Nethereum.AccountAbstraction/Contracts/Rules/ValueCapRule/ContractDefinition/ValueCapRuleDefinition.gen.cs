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
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapRule.ContractDefinition;

namespace Nethereum.AccountAbstraction.Contracts.Rules.ValueCapRule.ContractDefinition
{


    public partial class ValueCapRuleDeployment : ValueCapRuleDeploymentBase
    {
        public ValueCapRuleDeployment() : base(BYTECODE) { }
        public ValueCapRuleDeployment(string byteCode) : base(byteCode) { }
    }

    public class ValueCapRuleDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60808060405234601557610120908161001a8239f35b5f80fdfe60806004361015600d575f80fd5b5f3560e01c631658af3714601f575f80fd5b3460e657602036600319011260e65760043567ffffffffffffffff811160e6573660238201121560e657806004013567ffffffffffffffff811160e657810136602482011160e6576040908290031260e6576020820190602460448201359101351115815260208252604082019180831067ffffffffffffffff84111760d2576080908360405260208452805180938160608401528383015e5f828483010152603f1992601f8019910116810103010190f35b634e487b7160e01b5f52604160045260245ffd5b5f80fdfea2646970667358221220e0ac49ed4ed146bd4832e5d0e34beff42f9e3f456bde4447fa3f724540bc4f9664736f6c634300081c0033";
        public ValueCapRuleDeploymentBase() : base(BYTECODE) { }
        public ValueCapRuleDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class EvaluateFunction : EvaluateFunctionBase { }

    [Function("evaluate", "bytes")]
    public class EvaluateFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "input", 1)]
        public virtual byte[] Input { get; set; }
    }

    public partial class EvaluateOutputDTO : EvaluateOutputDTOBase { }

    [FunctionOutput]
    public class EvaluateOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes", "output", 1)]
        public virtual byte[] Output { get; set; }
    }
}
