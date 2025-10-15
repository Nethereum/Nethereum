using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.Uniswap.UniversalRouter.ContractDefinition
{


    public partial class UniversalRouterDeployment : UniversalRouterDeploymentBase
    {
        public UniversalRouterDeployment() : base(BYTECODE) { }
        public UniversalRouterDeployment(string byteCode) : base(byteCode) { }
    }

    public class UniversalRouterDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public UniversalRouterDeploymentBase() : base(BYTECODE) { }
        public UniversalRouterDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("tuple", "params", 1)]
        public virtual RouterParameters Params { get; set; }
    }

    public partial class V3PositionManagerFunction : V3PositionManagerFunctionBase { }

    [Function("V3_POSITION_MANAGER", "address")]
    public class V3PositionManagerFunctionBase : FunctionMessage
    {

    }

    public partial class V4PositionManagerFunction : V4PositionManagerFunctionBase { }

    [Function("V4_POSITION_MANAGER", "address")]
    public class V4PositionManagerFunctionBase : FunctionMessage
    {

    }

    public partial class ExecuteFunction : ExecuteFunctionBase { }

    [Function("execute")]
    public class ExecuteFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "commands", 1)]
        public virtual byte[] Commands { get; set; }
        [Parameter("bytes[]", "inputs", 2)]
        public virtual List<byte[]> Inputs { get; set; }
    }

    public partial class Execute1Function : Execute1FunctionBase { }

    [Function("execute")]
    public class Execute1FunctionBase : FunctionMessage
    {
        [Parameter("bytes", "commands", 1)]
        public virtual byte[] Commands { get; set; }
        [Parameter("bytes[]", "inputs", 2)]
        public virtual List<byte[]> Inputs { get; set; }
        [Parameter("uint256", "deadline", 3)]
        public virtual BigInteger Deadline { get; set; }
    }

    public partial class MsgSenderFunction : MsgSenderFunctionBase { }

    [Function("msgSender", "address")]
    public class MsgSenderFunctionBase : FunctionMessage
    {

    }

    public partial class PoolManagerFunction : PoolManagerFunctionBase { }

    [Function("poolManager", "address")]
    public class PoolManagerFunctionBase : FunctionMessage
    {

    }

    public partial class UniswapV3SwapCallbackFunction : UniswapV3SwapCallbackFunctionBase { }

    [Function("uniswapV3SwapCallback")]
    public class UniswapV3SwapCallbackFunctionBase : FunctionMessage
    {
        [Parameter("int256", "amount0Delta", 1)]
        public virtual BigInteger Amount0Delta { get; set; }
        [Parameter("int256", "amount1Delta", 2)]
        public virtual BigInteger Amount1Delta { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
    }

    public partial class UnlockCallbackFunction : UnlockCallbackFunctionBase { }

    [Function("unlockCallback", "bytes")]
    public class UnlockCallbackFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "data", 1)]
        public virtual byte[] Data { get; set; }
    }

    public partial class BalanceTooLowError : BalanceTooLowErrorBase { }
    [Error("BalanceTooLow")]
    public class BalanceTooLowErrorBase : IErrorDTO
    {
    }

    public partial class ContractLockedError : ContractLockedErrorBase { }
    [Error("ContractLocked")]
    public class ContractLockedErrorBase : IErrorDTO
    {
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

    public partial class ETHNotAcceptedError : ETHNotAcceptedErrorBase { }
    [Error("ETHNotAccepted")]
    public class ETHNotAcceptedErrorBase : IErrorDTO
    {
    }

    public partial class ExecutionFailedError : ExecutionFailedErrorBase { }

    [Error("ExecutionFailed")]
    public class ExecutionFailedErrorBase : IErrorDTO
    {
        [Parameter("uint256", "commandIndex", 1)]
        public virtual BigInteger CommandIndex { get; set; }
        [Parameter("bytes", "message", 2)]
        public virtual byte[] Message { get; set; }
    }

    public partial class FromAddressIsNotOwnerError : FromAddressIsNotOwnerErrorBase { }
    [Error("FromAddressIsNotOwner")]
    public class FromAddressIsNotOwnerErrorBase : IErrorDTO
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

    public partial class InsufficientETHError : InsufficientETHErrorBase { }
    [Error("InsufficientETH")]
    public class InsufficientETHErrorBase : IErrorDTO
    {
    }

    public partial class InsufficientTokenError : InsufficientTokenErrorBase { }
    [Error("InsufficientToken")]
    public class InsufficientTokenErrorBase : IErrorDTO
    {
    }

    public partial class InvalidActionError : InvalidActionErrorBase { }

    [Error("InvalidAction")]
    public class InvalidActionErrorBase : IErrorDTO
    {
        [Parameter("bytes4", "action", 1)]
        public virtual byte[] Action { get; set; }
    }

    public partial class InvalidBipsError : InvalidBipsErrorBase { }
    [Error("InvalidBips")]
    public class InvalidBipsErrorBase : IErrorDTO
    {
    }

    public partial class InvalidCommandTypeError : InvalidCommandTypeErrorBase { }

    [Error("InvalidCommandType")]
    public class InvalidCommandTypeErrorBase : IErrorDTO
    {
        [Parameter("uint256", "commandType", 1)]
        public virtual BigInteger CommandType { get; set; }
    }

    public partial class InvalidEthSenderError : InvalidEthSenderErrorBase { }
    [Error("InvalidEthSender")]
    public class InvalidEthSenderErrorBase : IErrorDTO
    {
    }

    public partial class InvalidPathError : InvalidPathErrorBase { }
    [Error("InvalidPath")]
    public class InvalidPathErrorBase : IErrorDTO
    {
    }

    public partial class InvalidReservesError : InvalidReservesErrorBase { }
    [Error("InvalidReserves")]
    public class InvalidReservesErrorBase : IErrorDTO
    {
    }

    public partial class LengthMismatchError : LengthMismatchErrorBase { }
    [Error("LengthMismatch")]
    public class LengthMismatchErrorBase : IErrorDTO
    {
    }

    public partial class NotAuthorizedForTokenError : NotAuthorizedForTokenErrorBase { }

    [Error("NotAuthorizedForToken")]
    public class NotAuthorizedForTokenErrorBase : IErrorDTO
    {
        [Parameter("uint256", "tokenId", 1)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class NotPoolManagerError : NotPoolManagerErrorBase { }
    [Error("NotPoolManager")]
    public class NotPoolManagerErrorBase : IErrorDTO
    {
    }

    public partial class OnlyMintAllowedError : OnlyMintAllowedErrorBase { }
    [Error("OnlyMintAllowed")]
    public class OnlyMintAllowedErrorBase : IErrorDTO
    {
    }

    public partial class SliceOutOfBoundsError : SliceOutOfBoundsErrorBase { }
    [Error("SliceOutOfBounds")]
    public class SliceOutOfBoundsErrorBase : IErrorDTO
    {
    }

    public partial class TransactionDeadlinePassedError : TransactionDeadlinePassedErrorBase { }
    [Error("TransactionDeadlinePassed")]
    public class TransactionDeadlinePassedErrorBase : IErrorDTO
    {
    }

    public partial class UnsafeCastError : UnsafeCastErrorBase { }
    [Error("UnsafeCast")]
    public class UnsafeCastErrorBase : IErrorDTO
    {
    }

    public partial class UnsupportedActionError : UnsupportedActionErrorBase { }

    [Error("UnsupportedAction")]
    public class UnsupportedActionErrorBase : IErrorDTO
    {
        [Parameter("uint256", "action", 1)]
        public virtual BigInteger Action { get; set; }
    }

    public partial class V2InvalidPathError : V2InvalidPathErrorBase { }
    [Error("V2InvalidPath")]
    public class V2InvalidPathErrorBase : IErrorDTO
    {
    }

    public partial class V2TooLittleReceivedError : V2TooLittleReceivedErrorBase { }
    [Error("V2TooLittleReceived")]
    public class V2TooLittleReceivedErrorBase : IErrorDTO
    {
    }

    public partial class V2TooMuchRequestedError : V2TooMuchRequestedErrorBase { }
    [Error("V2TooMuchRequested")]
    public class V2TooMuchRequestedErrorBase : IErrorDTO
    {
    }

    public partial class V3InvalidAmountOutError : V3InvalidAmountOutErrorBase { }
    [Error("V3InvalidAmountOut")]
    public class V3InvalidAmountOutErrorBase : IErrorDTO
    {
    }

    public partial class V3InvalidCallerError : V3InvalidCallerErrorBase { }
    [Error("V3InvalidCaller")]
    public class V3InvalidCallerErrorBase : IErrorDTO
    {
    }

    public partial class V3InvalidSwapError : V3InvalidSwapErrorBase { }
    [Error("V3InvalidSwap")]
    public class V3InvalidSwapErrorBase : IErrorDTO
    {
    }

    public partial class V3TooLittleReceivedError : V3TooLittleReceivedErrorBase { }
    [Error("V3TooLittleReceived")]
    public class V3TooLittleReceivedErrorBase : IErrorDTO
    {
    }

    public partial class V3TooMuchRequestedError : V3TooMuchRequestedErrorBase { }
    [Error("V3TooMuchRequested")]
    public class V3TooMuchRequestedErrorBase : IErrorDTO
    {
    }

    public partial class V4TooLittleReceivedError : V4TooLittleReceivedErrorBase { }

    [Error("V4TooLittleReceived")]
    public class V4TooLittleReceivedErrorBase : IErrorDTO
    {
        [Parameter("uint256", "minAmountOutReceived", 1)]
        public virtual BigInteger MinAmountOutReceived { get; set; }
        [Parameter("uint256", "amountReceived", 2)]
        public virtual BigInteger AmountReceived { get; set; }
    }

    public partial class V4TooMuchRequestedError : V4TooMuchRequestedErrorBase { }

    [Error("V4TooMuchRequested")]
    public class V4TooMuchRequestedErrorBase : IErrorDTO
    {
        [Parameter("uint256", "maxAmountInRequested", 1)]
        public virtual BigInteger MaxAmountInRequested { get; set; }
        [Parameter("uint256", "amountRequested", 2)]
        public virtual BigInteger AmountRequested { get; set; }
    }

    public partial class V3PositionManagerOutputDTO : V3PositionManagerOutputDTOBase { }

    [FunctionOutput]
    public class V3PositionManagerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class V4PositionManagerOutputDTO : V4PositionManagerOutputDTOBase { }

    [FunctionOutput]
    public class V4PositionManagerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }





    public partial class MsgSenderOutputDTO : MsgSenderOutputDTOBase { }

    [FunctionOutput]
    public class MsgSenderOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class PoolManagerOutputDTO : PoolManagerOutputDTOBase { }

    [FunctionOutput]
    public class PoolManagerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }




}
