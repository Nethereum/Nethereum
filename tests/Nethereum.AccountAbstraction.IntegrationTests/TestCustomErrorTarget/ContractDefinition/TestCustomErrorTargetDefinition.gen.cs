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
using Nethereum.AccountAbstraction.IntegrationTests.TestCustomErrorTarget.ContractDefinition;

namespace Nethereum.AccountAbstraction.IntegrationTests.TestCustomErrorTarget.ContractDefinition
{


    public partial class TestCustomErrorTargetDeployment : TestCustomErrorTargetDeploymentBase
    {
        public TestCustomErrorTargetDeployment() : base(BYTECODE) { }
        public TestCustomErrorTargetDeployment(string byteCode) : base(byteCode) { }
    }

    public class TestCustomErrorTargetDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "6080604052348015600e575f5ffd5b5060f58061001b5f395ff3fe6080604052348015600e575f5ffd5b50600436106030575f3560e01c806319159e6e146034578063e7cb5232146045575b5f5ffd5b6043603f36600460a9565b604b565b005b60436072565b60405163fc9f1dc160e01b8152600481018290523360248201526044015b60405180910390fd5b60405162461bcd60e51b815260206004820152600d60248201526c1cdd185b99185c990819985a5b609a1b60448201526064016069565b5f6020828403121560b8575f5ffd5b503591905056fea264697066735822122040b39fc835c1cb38d148625870a05cc674f848d09b36a2d9532e0122427fb4d864736f6c634300081c0033";
        public TestCustomErrorTargetDeploymentBase() : base(BYTECODE) { }
        public TestCustomErrorTargetDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class BookSlotFunction : BookSlotFunctionBase { }

    [Function("bookSlot")]
    public class BookSlotFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "slotId", 1)]
        public virtual BigInteger SlotId { get; set; }
    }

    public partial class FailWithReasonFunction : FailWithReasonFunctionBase { }

    [Function("failWithReason")]
    public class FailWithReasonFunctionBase : FunctionMessage
    {

    }





    public partial class SlotTakenError : SlotTakenErrorBase { }

    [Error("SlotTaken")]
    public class SlotTakenErrorBase : IErrorDTO
    {
        [Parameter("uint256", "slotId", 1)]
        public virtual BigInteger SlotId { get; set; }
        [Parameter("address", "caller", 2)]
        public virtual string Caller { get; set; }
    }
}
