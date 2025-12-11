using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;

namespace Nethereum.Contracts.Standards.EIP3009.ContractDefinition
{
    /// <summary>
    /// EIP-3009: Transfer With Authorization
    /// Allows transferring of fungible assets via a signed authorization
    /// https://eips.ethereum.org/EIPS/eip-3009
    /// </summary>
    public partial class TransferWithAuthorizationFunction : TransferWithAuthorizationFunctionBase { }

    [Function("transferWithAuthorization")]
    public class TransferWithAuthorizationFunctionBase : FunctionMessage
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
        public virtual byte[] Nonce { get; set; }

        [Parameter("uint8", "v", 7)]
        public virtual byte V { get; set; }

        [Parameter("bytes32", "r", 8)]
        public virtual byte[] R { get; set; }

        [Parameter("bytes32", "s", 9)]
        public virtual byte[] S { get; set; }
    }

    /// <summary>
    /// EIP-3009: Receive With Authorization
    /// Execute a transfer with a signed authorization from the payer
    /// This is the original USDC variant
    /// </summary>
    public partial class ReceiveWithAuthorizationFunction : ReceiveWithAuthorizationFunctionBase { }

    [Function("receiveWithAuthorization")]
    public class ReceiveWithAuthorizationFunctionBase : FunctionMessage
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
        public virtual byte[] Nonce { get; set; }

        [Parameter("uint8", "v", 7)]
        public virtual byte V { get; set; }

        [Parameter("bytes32", "r", 8)]
        public virtual byte[] R { get; set; }

        [Parameter("bytes32", "s", 9)]
        public virtual byte[] S { get; set; }
    }

    /// <summary>
    /// EIP-3009: Cancel Authorization
    /// Cancel an authorization
    /// </summary>
    public partial class CancelAuthorizationFunction : CancelAuthorizationFunctionBase { }

    [Function("cancelAuthorization")]
    public class CancelAuthorizationFunctionBase : FunctionMessage
    {
        [Parameter("address", "authorizer", 1)]
        public virtual string Authorizer { get; set; }

        [Parameter("bytes32", "nonce", 2)]
        public virtual byte[] Nonce { get; set; }

        [Parameter("uint8", "v", 3)]
        public virtual byte V { get; set; }

        [Parameter("bytes32", "r", 4)]
        public virtual byte[] R { get; set; }

        [Parameter("bytes32", "s", 5)]
        public virtual byte[] S { get; set; }
    }

    /// <summary>
    /// EIP-3009: Authorization State Query
    /// Check if an authorization has been used
    /// </summary>
    public partial class AuthorizationStateFunction : AuthorizationStateFunctionBase { }

    [Function("authorizationState", "bool")]
    public class AuthorizationStateFunctionBase : FunctionMessage
    {
        [Parameter("address", "authorizer", 1)]
        public virtual string Authorizer { get; set; }

        [Parameter("bytes32", "nonce", 2)]
        public virtual byte[] Nonce { get; set; }
    }

    public partial class AuthorizationStateOutputDTO : AuthorizationStateOutputDTOBase { }

    [FunctionOutput]
    public class AuthorizationStateOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool IsUsed { get; set; }
    }

    /// <summary>
    /// EIP-3009: Authorization Used Event
    /// Emitted when an authorization is used
    /// </summary>
    public partial class AuthorizationUsedEventDTO : AuthorizationUsedEventDTOBase { }

    [Event("AuthorizationUsed")]
    public class AuthorizationUsedEventDTOBase : IEventDTO
    {
        [Parameter("address", "authorizer", 1, true)]
        public virtual string Authorizer { get; set; }

        [Parameter("bytes32", "nonce", 2, true)]
        public virtual byte[] Nonce { get; set; }
    }

    /// <summary>
    /// EIP-3009: Authorization Canceled Event
    /// Emitted when an authorization is canceled
    /// </summary>
    public partial class AuthorizationCanceledEventDTO : AuthorizationCanceledEventDTOBase { }

    [Event("AuthorizationCanceled")]
    public class AuthorizationCanceledEventDTOBase : IEventDTO
    {
        [Parameter("address", "authorizer", 1, true)]
        public virtual string Authorizer { get; set; }

        [Parameter("bytes32", "nonce", 2, true)]
        public virtual byte[] Nonce { get; set; }
    }
}
