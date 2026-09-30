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
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget.ContractDefinition;

namespace Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget.ContractDefinition
{


    public partial class PayableTargetDeployment : PayableTargetDeploymentBase
    {
        public PayableTargetDeployment() : base(BYTECODE) { }
        public PayableTargetDeployment(string byteCode) : base(byteCode) { }
    }

    public class PayableTargetDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x608080604052346013576063908160188239f35b5f80fdfe6004361015600b575f80fd5b5f3560e01c63d0e30db014601d575f80fd5b5f366003190112602957005b5f80fdfea264697066735822122050e1c535f26d2d9c1762bfeec872710e88aab7313c5fc701c0af9e8f14e225ca64736f6c634300081c0033";
        public PayableTargetDeploymentBase() : base(BYTECODE) { }
        public PayableTargetDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class DepositFunction : DepositFunctionBase { }

    [Function("deposit")]
    public class DepositFunctionBase : FunctionMessage
    {

    }


}
