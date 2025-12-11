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

namespace Nethereum.Contracts.EIP3009.EIP3009.ContractDefinition
{


    public partial class Eip3009Deployment : Eip3009DeploymentBase
    {
        public Eip3009Deployment() : base(BYTECODE) { }
        public Eip3009Deployment(string byteCode) : base(byteCode) { }
    }

    public class Eip3009DeploymentBase : ContractDeploymentMessage
    {
        // Note: EIP-3009 is an interface/standard, not a deployable contract
        // Tokens like USDC implement this standard
        public static string BYTECODE = "0x";
        public Eip3009DeploymentBase() : base(BYTECODE) { }
        public Eip3009DeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class TransferWithAuthorization1Function : TransferWithAuthorization1FunctionBase { }

    [Function("transferWithAuthorization")]
    public class TransferWithAuthorization1FunctionBase : FunctionMessage
    {
        [Parameter("address", "from", 1)]
        public virtual string AuthorisationFrom { get; set; }
        [Parameter("address", "to", 2)]
        public virtual string AuthorisationTo { get; set; }
        [Parameter("uint256", "value", 3)]
        public virtual BigInteger Value { get; set; }
        [Parameter("uint256", "validAfter", 4)]
        public virtual BigInteger ValidAfter { get; set; }
        [Parameter("uint256", "validBefore", 5)]
        public virtual BigInteger ValidBefore { get; set; }
        [Parameter("bytes32", "nonce", 6)]
        public virtual byte[] AuthorisationNonce { get; set; }
        [Parameter("uint8", "v", 7)]
        public virtual byte V { get; set; }
        [Parameter("bytes32", "r", 8)]
        public virtual byte[] R { get; set; }
        [Parameter("bytes32", "s", 9)]
        public virtual byte[] S { get; set; }
    }

    public partial class ReceiveWithAuthorization1Function : ReceiveWithAuthorization1FunctionBase { }

    [Function("receiveWithAuthorization")]
    public class ReceiveWithAuthorization1FunctionBase : FunctionMessage
    {
        [Parameter("address", "from", 1)]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2)]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 3)]
        public virtual BigInteger Value { get; set; }
        [Parameter("uint256", "validAfter", 4)]
        public virtual BigInteger ValidAfter { get; set; }
        [Parameter("uint256", "validBefore", 5)]
        public virtual BigInteger ValidBefore { get; set; }
        [Parameter("bytes32", "nonce", 6)]
        public virtual byte[] AuthorisationNonce { get; set; }
        [Parameter("uint8", "v", 7)]
        public virtual byte V { get; set; }
        [Parameter("bytes32", "r", 8)]
        public virtual byte[] R { get; set; }
        [Parameter("bytes32", "s", 9)]
        public virtual byte[] S { get; set; }
    }

    public partial class CancelAuthorization1Function : CancelAuthorization1FunctionBase { }

    [Function("cancelAuthorization")]
    public class CancelAuthorization1FunctionBase : FunctionMessage
    {
        [Parameter("address", "authorizer", 1)]
        public virtual string Authorizer { get; set; }
        [Parameter("bytes32", "nonce", 2)]
        public virtual byte[] AuthorisationNonce { get; set; }
        [Parameter("uint8", "v", 3)]
        public virtual byte V { get; set; }
        [Parameter("bytes32", "r", 4)]
        public virtual byte[] R { get; set; }
        [Parameter("bytes32", "s", 5)]
        public virtual byte[] S { get; set; }
    }

    public partial class AuthorizationStateFunction : AuthorizationStateFunctionBase { }

    [Function("authorizationState", "bool")]
    public class AuthorizationStateFunctionBase : FunctionMessage
    {
        [Parameter("address", "authorizer", 1)]
        public virtual string Authorizer { get; set; }
        [Parameter("bytes32", "nonce", 2)]
        public virtual byte[] AuthorisationNonce { get; set; }
    }

    public partial class AuthorizationUsedEventDTO : AuthorizationUsedEventDTOBase { }

    [Event("AuthorizationUsed")]
    public class AuthorizationUsedEventDTOBase : IEventDTO
    {
        [Parameter("address", "authorizer", 1, true )]
        public virtual string Authorizer { get; set; }
        [Parameter("bytes32", "nonce", 2, true )]
        public virtual byte[] AuthorisationNonce { get; set; }
    }

    public partial class AuthorizationCanceledEventDTO : AuthorizationCanceledEventDTOBase { }

    [Event("AuthorizationCanceled")]
    public class AuthorizationCanceledEventDTOBase : IEventDTO
    {
        [Parameter("address", "authorizer", 1, true )]
        public virtual string Authorizer { get; set; }
        [Parameter("bytes32", "nonce", 2, true )]
        public virtual byte[] AuthorisationNonce { get; set; }
    }













    public partial class AuthorizationStateOutputDTO : AuthorizationStateOutputDTOBase { }

    [FunctionOutput]
    public class AuthorizationStateOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }
}
