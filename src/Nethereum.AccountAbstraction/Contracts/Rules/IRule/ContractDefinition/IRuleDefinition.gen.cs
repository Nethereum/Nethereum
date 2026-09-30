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
using Nethereum.AccountAbstraction.Contracts.Rules.IRule.ContractDefinition;

namespace Nethereum.AccountAbstraction.Contracts.Rules.IRule.ContractDefinition
{


    public partial class IRuleDeployment : IRuleDeploymentBase
    {
        public IRuleDeployment() : base(BYTECODE) { }
        public IRuleDeployment(string byteCode) : base(byteCode) { }
    }

    public class IRuleDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public IRuleDeploymentBase() : base(BYTECODE) { }
        public IRuleDeploymentBase(string byteCode) : base(byteCode) { }

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
