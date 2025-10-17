using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.Uniswap.V4.Positions.PositionManager.ContractDefinition
{


    public partial class PositionManagerDeployment : PositionManagerDeploymentBase
    {
        public PositionManagerDeployment() : base(BYTECODE) { }
        public PositionManagerDeployment(string byteCode) : base(byteCode) { }
    }

    public class PositionManagerDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public PositionManagerDeploymentBase() : base(BYTECODE) { }
        public PositionManagerDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_poolManager", 1)]
        public virtual string PoolManager { get; set; }
        [Parameter("address", "_permit2", 2)]
        public virtual string Permit2 { get; set; }
        [Parameter("uint256", "_unsubscribeGasLimit", 3)]
        public virtual BigInteger UnsubscribeGasLimit { get; set; }
        [Parameter("address", "_tokenDescriptor", 4)]
        public virtual string TokenDescriptor { get; set; }
        [Parameter("address", "_weth9", 5)]
        public virtual string Weth9 { get; set; }
    }

    public partial class DomainSeparatorFunction : DomainSeparatorFunctionBase { }

    [Function("DOMAIN_SEPARATOR", "bytes32")]
    public class DomainSeparatorFunctionBase : FunctionMessage
    {

    }

    public partial class Weth9Function : Weth9FunctionBase { }

    [Function("WETH9", "address")]
    public class Weth9FunctionBase : FunctionMessage
    {

    }

    public partial class ApproveFunction : ApproveFunctionBase { }

    [Function("approve")]
    public class ApproveFunctionBase : FunctionMessage
    {
        [Parameter("address", "spender", 1)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "id", 2)]
        public virtual BigInteger Id { get; set; }
    }

    public partial class BalanceOfFunction : BalanceOfFunctionBase { }

    [Function("balanceOf", "uint256")]
    public class BalanceOfFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class GetApprovedFunction : GetApprovedFunctionBase { }

    [Function("getApproved", "address")]
    public class GetApprovedFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetPoolAndPositionInfoFunction : GetPoolAndPositionInfoFunctionBase { }

    [Function("getPoolAndPositionInfo", typeof(GetPoolAndPositionInfoOutputDTO))]
    public class GetPoolAndPositionInfoFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "tokenId", 1)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class GetPositionLiquidityFunction : GetPositionLiquidityFunctionBase { }

    [Function("getPositionLiquidity", "uint128")]
    public class GetPositionLiquidityFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "tokenId", 1)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class InitializePoolFunction : InitializePoolFunctionBase { }

    [Function("initializePool", "int24")]
    public class InitializePoolFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "key", 1)]
        public virtual PoolKey Key { get; set; }
        [Parameter("uint160", "sqrtPriceX96", 2)]
        public virtual BigInteger SqrtPriceX96 { get; set; }
    }

    public partial class IsApprovedForAllFunction : IsApprovedForAllFunctionBase { }

    [Function("isApprovedForAll", "bool")]
    public class IsApprovedForAllFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("address", "", 2)]
        public virtual string ReturnValue2 { get; set; }
    }

    public partial class ModifyLiquiditiesFunction : ModifyLiquiditiesFunctionBase { }

    [Function("modifyLiquidities")]
    public class ModifyLiquiditiesFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "unlockData", 1)]
        public virtual byte[] UnlockData { get; set; }
        [Parameter("uint256", "deadline", 2)]
        public virtual BigInteger Deadline { get; set; }
    }

    public partial class ModifyLiquiditiesWithoutUnlockFunction : ModifyLiquiditiesWithoutUnlockFunctionBase { }

    [Function("modifyLiquiditiesWithoutUnlock")]
    public class ModifyLiquiditiesWithoutUnlockFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "actions", 1)]
        public virtual byte[] Actions { get; set; }
        [Parameter("bytes[]", "params", 2)]
        public virtual List<byte[]> Params { get; set; }
    }

    public partial class MsgSenderFunction : MsgSenderFunctionBase { }

    [Function("msgSender", "address")]
    public class MsgSenderFunctionBase : FunctionMessage
    {

    }

    public partial class MulticallFunction : MulticallFunctionBase { }

    [Function("multicall", "bytes[]")]
    public class MulticallFunctionBase : FunctionMessage
    {
        [Parameter("bytes[]", "data", 1)]
        public virtual List<byte[]> Data { get; set; }
    }

    public partial class NameFunction : NameFunctionBase { }

    [Function("name", "string")]
    public class NameFunctionBase : FunctionMessage
    {

    }

    public partial class NextTokenIdFunction : NextTokenIdFunctionBase { }

    [Function("nextTokenId", "uint256")]
    public class NextTokenIdFunctionBase : FunctionMessage
    {

    }

    public partial class NoncesFunction : NoncesFunctionBase { }

    [Function("nonces", "uint256")]
    public class NoncesFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("uint256", "word", 2)]
        public virtual BigInteger Word { get; set; }
    }

    public partial class OwnerOfFunction : OwnerOfFunctionBase { }

    [Function("ownerOf", "address")]
    public class OwnerOfFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "id", 1)]
        public virtual BigInteger Id { get; set; }
    }

    public partial class Permit1Function : Permit1FunctionBase { }

    [Function("permit")]
    public class Permit1FunctionBase : FunctionMessage
    {
        [Parameter("address", "spender", 1)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "tokenId", 2)]
        public virtual BigInteger TokenId { get; set; }
        [Parameter("uint256", "deadline", 3)]
        public virtual BigInteger Deadline { get; set; }
        [Parameter("uint256", "nonce", 4)]
        public virtual BigInteger PermitNonce { get; set; }
        [Parameter("bytes", "signature", 5)]
        public virtual byte[] Signature { get; set; }
    }

    public partial class PermitFunction : PermitFunctionBase { }

    [Function("permit", "bytes")]
    public class PermitFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("tuple", "permitSingle", 2)]
        public virtual PermitSingle PermitSingle { get; set; }
        [Parameter("bytes", "signature", 3)]
        public virtual byte[] Signature { get; set; }
    }

    public partial class Permit2Function : Permit2FunctionBase { }

    [Function("permit2", "address")]
    public class Permit2FunctionBase : FunctionMessage
    {

    }

    public partial class PermitBatchFunction : PermitBatchFunctionBase { }

    [Function("permitBatch", "bytes")]
    public class PermitBatchFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("tuple", "_permitBatch", 2)]
        public virtual PermitBatch PermitBatch { get; set; }
        [Parameter("bytes", "signature", 3)]
        public virtual byte[] Signature { get; set; }
    }

    public partial class PermitForAllFunction : PermitForAllFunctionBase { }

    [Function("permitForAll")]
    public class PermitForAllFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("address", "operator", 2)]
        public virtual string Operator { get; set; }
        [Parameter("bool", "approved", 3)]
        public virtual bool Approved { get; set; }
        [Parameter("uint256", "deadline", 4)]
        public virtual BigInteger Deadline { get; set; }
        [Parameter("uint256", "nonce", 5)]
        public virtual BigInteger PermitNonce { get; set; }
        [Parameter("bytes", "signature", 6)]
        public virtual byte[] Signature { get; set; }
    }

    public partial class PoolKeysFunction : PoolKeysFunctionBase { }

    [Function("poolKeys", typeof(PoolKeysOutputDTO))]
    public class PoolKeysFunctionBase : FunctionMessage
    {
        [Parameter("bytes25", "poolId", 1)]
        public virtual byte[] PoolId { get; set; }
    }

    public partial class PoolManagerFunction : PoolManagerFunctionBase { }

    [Function("poolManager", "address")]
    public class PoolManagerFunctionBase : FunctionMessage
    {

    }

    public partial class PositionInfoFunction : PositionInfoFunctionBase { }

    [Function("positionInfo", "uint256")]
    public class PositionInfoFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "tokenId", 1)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class RevokeNonceFunction : RevokeNonceFunctionBase { }

    [Function("revokeNonce")]
    public class RevokeNonceFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "nonce", 1)]
        public virtual BigInteger RevokeNonce { get; set; }
    }

    public partial class SafeTransferFromFunction : SafeTransferFromFunctionBase { }

    [Function("safeTransferFrom")]
    public class SafeTransferFromFunctionBase : FunctionMessage
    {
        [Parameter("address", "from", 1)]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2)]
        public virtual string To { get; set; }
        [Parameter("uint256", "id", 3)]
        public virtual BigInteger Id { get; set; }
    }

    public partial class SafeTransferFrom1Function : SafeTransferFrom1FunctionBase { }

    [Function("safeTransferFrom")]
    public class SafeTransferFrom1FunctionBase : FunctionMessage
    {
        [Parameter("address", "from", 1)]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2)]
        public virtual string To { get; set; }
        [Parameter("uint256", "id", 3)]
        public virtual BigInteger Id { get; set; }
        [Parameter("bytes", "data", 4)]
        public virtual byte[] Data { get; set; }
    }

    public partial class SetApprovalForAllFunction : SetApprovalForAllFunctionBase { }

    [Function("setApprovalForAll")]
    public class SetApprovalForAllFunctionBase : FunctionMessage
    {
        [Parameter("address", "operator", 1)]
        public virtual string Operator { get; set; }
        [Parameter("bool", "approved", 2)]
        public virtual bool Approved { get; set; }
    }

    public partial class SubscribeFunction : SubscribeFunctionBase { }

    [Function("subscribe")]
    public class SubscribeFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "tokenId", 1)]
        public virtual BigInteger TokenId { get; set; }
        [Parameter("address", "newSubscriber", 2)]
        public virtual string NewSubscriber { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
    }

    public partial class SubscriberFunction : SubscriberFunctionBase { }

    [Function("subscriber", "address")]
    public class SubscriberFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "tokenId", 1)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceId", 1)]
        public virtual byte[] InterfaceId { get; set; }
    }

    public partial class SymbolFunction : SymbolFunctionBase { }

    [Function("symbol", "string")]
    public class SymbolFunctionBase : FunctionMessage
    {

    }

    public partial class TokenDescriptorFunction : TokenDescriptorFunctionBase { }

    [Function("tokenDescriptor", "address")]
    public class TokenDescriptorFunctionBase : FunctionMessage
    {

    }

    public partial class TokenURIFunction : TokenURIFunctionBase { }

    [Function("tokenURI", "string")]
    public class TokenURIFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "tokenId", 1)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class TransferFromFunction : TransferFromFunctionBase { }

    [Function("transferFrom")]
    public class TransferFromFunctionBase : FunctionMessage
    {
        [Parameter("address", "from", 1)]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2)]
        public virtual string To { get; set; }
        [Parameter("uint256", "id", 3)]
        public virtual BigInteger Id { get; set; }
    }

    public partial class UnlockCallbackFunction : UnlockCallbackFunctionBase { }

    [Function("unlockCallback", "bytes")]
    public class UnlockCallbackFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "data", 1)]
        public virtual byte[] Data { get; set; }
    }

    public partial class UnsubscribeFunction : UnsubscribeFunctionBase { }

    [Function("unsubscribe")]
    public class UnsubscribeFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "tokenId", 1)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class UnsubscribeGasLimitFunction : UnsubscribeGasLimitFunctionBase { }

    [Function("unsubscribeGasLimit", "uint256")]
    public class UnsubscribeGasLimitFunctionBase : FunctionMessage
    {

    }

    public partial class ApprovalEventDTO : ApprovalEventDTOBase { }

    [Event("Approval")]
    public class ApprovalEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true )]
        public virtual string Owner { get; set; }
        [Parameter("address", "spender", 2, true )]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "id", 3, true )]
        public virtual BigInteger Id { get; set; }
    }

    public partial class ApprovalForAllEventDTO : ApprovalForAllEventDTOBase { }

    [Event("ApprovalForAll")]
    public class ApprovalForAllEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true )]
        public virtual string Owner { get; set; }
        [Parameter("address", "operator", 2, true )]
        public virtual string Operator { get; set; }
        [Parameter("bool", "approved", 3, false )]
        public virtual bool Approved { get; set; }
    }

    public partial class SubscriptionEventDTO : SubscriptionEventDTOBase { }

    [Event("Subscription")]
    public class SubscriptionEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "tokenId", 1, true )]
        public virtual BigInteger TokenId { get; set; }
        [Parameter("address", "subscriber", 2, true )]
        public virtual string Subscriber { get; set; }
    }

    public partial class TransferEventDTO : TransferEventDTOBase { }

    [Event("Transfer")]
    public class TransferEventDTOBase : IEventDTO
    {
        [Parameter("address", "from", 1, true )]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2, true )]
        public virtual string To { get; set; }
        [Parameter("uint256", "id", 3, true )]
        public virtual BigInteger Id { get; set; }
    }

    public partial class UnsubscriptionEventDTO : UnsubscriptionEventDTOBase { }

    [Event("Unsubscription")]
    public class UnsubscriptionEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "tokenId", 1, true )]
        public virtual BigInteger TokenId { get; set; }
        [Parameter("address", "subscriber", 2, true )]
        public virtual string Subscriber { get; set; }
    }

    public partial class AlreadySubscribedError : AlreadySubscribedErrorBase { }

    [Error("AlreadySubscribed")]
    public class AlreadySubscribedErrorBase : IErrorDTO
    {
        [Parameter("uint256", "tokenId", 1)]
        public virtual BigInteger TokenId { get; set; }
        [Parameter("address", "subscriber", 2)]
        public virtual string Subscriber { get; set; }
    }

    public partial class BurnNotificationRevertedError : BurnNotificationRevertedErrorBase { }

    [Error("BurnNotificationReverted")]
    public class BurnNotificationRevertedErrorBase : IErrorDTO
    {
        [Parameter("address", "subscriber", 1)]
        public virtual string Subscriber { get; set; }
        [Parameter("bytes", "reason", 2)]
        public virtual byte[] Reason { get; set; }
    }

    public partial class ContractLockedError : ContractLockedErrorBase { }
    [Error("ContractLocked")]
    public class ContractLockedErrorBase : IErrorDTO
    {
    }

    public partial class DeadlinePassedError : DeadlinePassedErrorBase { }

    [Error("DeadlinePassed")]
    public class DeadlinePassedErrorBase : IErrorDTO
    {
        [Parameter("uint256", "deadline", 1)]
        public virtual BigInteger Deadline { get; set; }
    }

    public partial class DeltaNotNegativeError : DeltaNotNegativeErrorBase { }

    [Error("DeltaNotNegative")]
    public class DeltaNotNegativeErrorBase : IErrorDTO
    {
        [Parameter("address", "currency", 1)]
        public virtual string Currency { get; set; }
    }

    public partial class DeltaNotPositiveError : DeltaNotPositiveErrorBase { }

    [Error("DeltaNotPositive")]
    public class DeltaNotPositiveErrorBase : IErrorDTO
    {
        [Parameter("address", "currency", 1)]
        public virtual string Currency { get; set; }
    }

    public partial class GasLimitTooLowError : GasLimitTooLowErrorBase { }
    [Error("GasLimitTooLow")]
    public class GasLimitTooLowErrorBase : IErrorDTO
    {
    }

    public partial class InputLengthMismatchError : InputLengthMismatchErrorBase { }
    [Error("InputLengthMismatch")]
    public class InputLengthMismatchErrorBase : IErrorDTO
    {
    }

    public partial class InsufficientBalanceError : InsufficientBalanceErrorBase { }
    [Error("InsufficientBalance")]
    public class InsufficientBalanceErrorBase : IErrorDTO
    {
    }

    public partial class InvalidContractSignatureError : InvalidContractSignatureErrorBase { }
    [Error("InvalidContractSignature")]
    public class InvalidContractSignatureErrorBase : IErrorDTO
    {
    }

    public partial class InvalidEthSenderError : InvalidEthSenderErrorBase { }
    [Error("InvalidEthSender")]
    public class InvalidEthSenderErrorBase : IErrorDTO
    {
    }

    public partial class InvalidSignatureError : InvalidSignatureErrorBase { }
    [Error("InvalidSignature")]
    public class InvalidSignatureErrorBase : IErrorDTO
    {
    }

    public partial class InvalidSignatureLengthError : InvalidSignatureLengthErrorBase { }
    [Error("InvalidSignatureLength")]
    public class InvalidSignatureLengthErrorBase : IErrorDTO
    {
    }

    public partial class InvalidSignerError : InvalidSignerErrorBase { }
    [Error("InvalidSigner")]
    public class InvalidSignerErrorBase : IErrorDTO
    {
    }

    public partial class MaximumAmountExceededError : MaximumAmountExceededErrorBase { }

    [Error("MaximumAmountExceeded")]
    public class MaximumAmountExceededErrorBase : IErrorDTO
    {
        [Parameter("uint128", "maximumAmount", 1)]
        public virtual BigInteger MaximumAmount { get; set; }
        [Parameter("uint128", "amountRequested", 2)]
        public virtual BigInteger AmountRequested { get; set; }
    }

    public partial class MinimumAmountInsufficientError : MinimumAmountInsufficientErrorBase { }

    [Error("MinimumAmountInsufficient")]
    public class MinimumAmountInsufficientErrorBase : IErrorDTO
    {
        [Parameter("uint128", "minimumAmount", 1)]
        public virtual BigInteger MinimumAmount { get; set; }
        [Parameter("uint128", "amountReceived", 2)]
        public virtual BigInteger AmountReceived { get; set; }
    }

    public partial class ModifyLiquidityNotificationRevertedError : ModifyLiquidityNotificationRevertedErrorBase { }

    [Error("ModifyLiquidityNotificationReverted")]
    public class ModifyLiquidityNotificationRevertedErrorBase : IErrorDTO
    {
        [Parameter("address", "subscriber", 1)]
        public virtual string Subscriber { get; set; }
        [Parameter("bytes", "reason", 2)]
        public virtual byte[] Reason { get; set; }
    }

    public partial class NoCodeSubscriberError : NoCodeSubscriberErrorBase { }
    [Error("NoCodeSubscriber")]
    public class NoCodeSubscriberErrorBase : IErrorDTO
    {
    }

    public partial class NoSelfPermitError : NoSelfPermitErrorBase { }
    [Error("NoSelfPermit")]
    public class NoSelfPermitErrorBase : IErrorDTO
    {
    }

    public partial class NonceAlreadyUsedError : NonceAlreadyUsedErrorBase { }
    [Error("NonceAlreadyUsed")]
    public class NonceAlreadyUsedErrorBase : IErrorDTO
    {
    }

    public partial class NotApprovedError : NotApprovedErrorBase { }

    [Error("NotApproved")]
    public class NotApprovedErrorBase : IErrorDTO
    {
        [Parameter("address", "caller", 1)]
        public virtual string Caller { get; set; }
    }

    public partial class NotPoolManagerError : NotPoolManagerErrorBase { }
    [Error("NotPoolManager")]
    public class NotPoolManagerErrorBase : IErrorDTO
    {
    }

    public partial class NotSubscribedError : NotSubscribedErrorBase { }
    [Error("NotSubscribed")]
    public class NotSubscribedErrorBase : IErrorDTO
    {
    }

    public partial class PoolManagerMustBeLockedError : PoolManagerMustBeLockedErrorBase { }
    [Error("PoolManagerMustBeLocked")]
    public class PoolManagerMustBeLockedErrorBase : IErrorDTO
    {
    }

    public partial class SignatureDeadlineExpiredError : SignatureDeadlineExpiredErrorBase { }
    [Error("SignatureDeadlineExpired")]
    public class SignatureDeadlineExpiredErrorBase : IErrorDTO
    {
    }

    public partial class SubscriptionRevertedError : SubscriptionRevertedErrorBase { }

    [Error("SubscriptionReverted")]
    public class SubscriptionRevertedErrorBase : IErrorDTO
    {
        [Parameter("address", "subscriber", 1)]
        public virtual string Subscriber { get; set; }
        [Parameter("bytes", "reason", 2)]
        public virtual byte[] Reason { get; set; }
    }

    public partial class UnauthorizedError : UnauthorizedErrorBase { }
    [Error("Unauthorized")]
    public class UnauthorizedErrorBase : IErrorDTO
    {
    }

    public partial class UnsupportedActionError : UnsupportedActionErrorBase { }

    [Error("UnsupportedAction")]
    public class UnsupportedActionErrorBase : IErrorDTO
    {
        [Parameter("uint256", "action", 1)]
        public virtual BigInteger Action { get; set; }
    }

    public partial class DomainSeparatorOutputDTO : DomainSeparatorOutputDTOBase { }

    [FunctionOutput]
    public class DomainSeparatorOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class Weth9OutputDTO : Weth9OutputDTOBase { }

    [FunctionOutput]
    public class Weth9OutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class BalanceOfOutputDTO : BalanceOfOutputDTOBase { }

    [FunctionOutput]
    public class BalanceOfOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetApprovedOutputDTO : GetApprovedOutputDTOBase { }

    [FunctionOutput]
    public class GetApprovedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GetPoolAndPositionInfoOutputDTO : GetPoolAndPositionInfoOutputDTOBase { }

    [FunctionOutput]
    public class GetPoolAndPositionInfoOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("tuple", "poolKey", 1)]
        public virtual PoolKey PoolKey { get; set; }
        [Parameter("uint256", "info", 2)]
        public virtual BigInteger Info { get; set; }
    }

    public partial class GetPositionLiquidityOutputDTO : GetPositionLiquidityOutputDTOBase { }

    [FunctionOutput]
    public class GetPositionLiquidityOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint128", "liquidity", 1)]
        public virtual BigInteger Liquidity { get; set; }
    }



    public partial class IsApprovedForAllOutputDTO : IsApprovedForAllOutputDTOBase { }

    [FunctionOutput]
    public class IsApprovedForAllOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }





    public partial class MsgSenderOutputDTO : MsgSenderOutputDTOBase { }

    [FunctionOutput]
    public class MsgSenderOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class NameOutputDTO : NameOutputDTOBase { }

    [FunctionOutput]
    public class NameOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class NextTokenIdOutputDTO : NextTokenIdOutputDTOBase { }

    [FunctionOutput]
    public class NextTokenIdOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class NoncesOutputDTO : NoncesOutputDTOBase { }

    [FunctionOutput]
    public class NoncesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "bitmap", 1)]
        public virtual BigInteger Bitmap { get; set; }
    }

    public partial class OwnerOfOutputDTO : OwnerOfOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOfOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
    }





    public partial class Permit2OutputDTO : Permit2OutputDTOBase { }

    [FunctionOutput]
    public class Permit2OutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }





    public partial class PoolKeysOutputDTO : PoolKeysOutputDTOBase { }

    [FunctionOutput]
    public class PoolKeysOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "currency0", 1)]
        public virtual string Currency0 { get; set; }
        [Parameter("address", "currency1", 2)]
        public virtual string Currency1 { get; set; }
        [Parameter("uint24", "fee", 3)]
        public virtual uint Fee { get; set; }
        [Parameter("int24", "tickSpacing", 4)]
        public virtual int TickSpacing { get; set; }
        [Parameter("address", "hooks", 5)]
        public virtual string Hooks { get; set; }
    }

    public partial class PoolManagerOutputDTO : PoolManagerOutputDTOBase { }

    [FunctionOutput]
    public class PoolManagerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class PositionInfoOutputDTO : PositionInfoOutputDTOBase { }

    [FunctionOutput]
    public class PositionInfoOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "info", 1)]
        public virtual BigInteger Info { get; set; }
    }











    public partial class SubscriberOutputDTO : SubscriberOutputDTOBase { }

    [FunctionOutput]
    public class SubscriberOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "subscriber", 1)]
        public virtual string Subscriber { get; set; }
    }

    public partial class SupportsInterfaceOutputDTO : SupportsInterfaceOutputDTOBase { }

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class SymbolOutputDTO : SymbolOutputDTOBase { }

    [FunctionOutput]
    public class SymbolOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class TokenDescriptorOutputDTO : TokenDescriptorOutputDTOBase { }

    [FunctionOutput]
    public class TokenDescriptorOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class TokenURIOutputDTO : TokenURIOutputDTOBase { }

    [FunctionOutput]
    public class TokenURIOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }







    public partial class UnsubscribeGasLimitOutputDTO : UnsubscribeGasLimitOutputDTOBase { }

    [FunctionOutput]
    public class UnsubscribeGasLimitOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }
}
