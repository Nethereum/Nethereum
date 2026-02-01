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
using Nethereum.AccountAbstraction.Contracts.Interfaces.IVerifyingPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.IVerifyingPaymaster.ContractDefinition
{


    public partial class IVerifyingPaymasterDeployment : IVerifyingPaymasterDeploymentBase
    {
        public IVerifyingPaymasterDeployment() : base(BYTECODE) { }
        public IVerifyingPaymasterDeployment(string byteCode) : base(byteCode) { }
    }

    public class IVerifyingPaymasterDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public IVerifyingPaymasterDeploymentBase() : base(BYTECODE) { }
        public IVerifyingPaymasterDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class PostOpFunction : PostOpFunctionBase { }

    [Function("postOp")]
    public class PostOpFunctionBase : FunctionMessage
    {
        [Parameter("uint8", "mode", 1)]
        public virtual byte Mode { get; set; }
        [Parameter("bytes", "context", 2)]
        public virtual byte[] Context { get; set; }
        [Parameter("uint256", "actualGasCost", 3)]
        public virtual BigInteger ActualGasCost { get; set; }
        [Parameter("uint256", "actualUserOpFeePerGas", 4)]
        public virtual BigInteger ActualUserOpFeePerGas { get; set; }
    }

    public partial class SetVerifyingSignerFunction : SetVerifyingSignerFunctionBase { }

    [Function("setVerifyingSigner")]
    public class SetVerifyingSignerFunctionBase : FunctionMessage
    {
        [Parameter("address", "signer", 1)]
        public virtual string Signer { get; set; }
    }

    public partial class ValidatePaymasterUserOpFunction : ValidatePaymasterUserOpFunctionBase { }

    [Function("validatePaymasterUserOp", typeof(ValidatePaymasterUserOpOutputDTO))]
    public class ValidatePaymasterUserOpFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "userOp", 1)]
        public virtual PackedUserOperation UserOp { get; set; }
        [Parameter("bytes32", "userOpHash", 2)]
        public virtual byte[] UserOpHash { get; set; }
        [Parameter("uint256", "maxCost", 3)]
        public virtual BigInteger MaxCost { get; set; }
    }

    public partial class VerifyingSignerFunction : VerifyingSignerFunctionBase { }

    [Function("verifyingSigner", "address")]
    public class VerifyingSignerFunctionBase : FunctionMessage
    {

    }





    public partial class ValidatePaymasterUserOpOutputDTO : ValidatePaymasterUserOpOutputDTOBase { }

    [FunctionOutput]
    public class ValidatePaymasterUserOpOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes", "context", 1)]
        public virtual byte[] Context { get; set; }
        [Parameter("uint256", "validationData", 2)]
        public virtual BigInteger ValidationData { get; set; }
    }

    public partial class VerifyingSignerOutputDTO : VerifyingSignerOutputDTOBase { }

    [FunctionOutput]
    public class VerifyingSignerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GasSponsoredEventDTO : GasSponsoredEventDTOBase { }

    [Event("GasSponsored")]
    public class GasSponsoredEventDTOBase : IEventDTO
    {
        [Parameter("address", "sender", 1, true )]
        public virtual string Sender { get; set; }
        [Parameter("uint256", "gasCost", 2, false )]
        public virtual BigInteger GasCost { get; set; }
    }
}
