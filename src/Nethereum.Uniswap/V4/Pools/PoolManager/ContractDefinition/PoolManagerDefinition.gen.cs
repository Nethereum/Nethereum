using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.Uniswap.V4.Pools.PoolManager.ContractDefinition
{


    public partial class PoolManagerDeployment : PoolManagerDeploymentBase
    {
        public PoolManagerDeployment() : base(BYTECODE) { }
        public PoolManagerDeployment(string byteCode) : base(byteCode) { }
    }

    public class PoolManagerDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public PoolManagerDeploymentBase() : base(BYTECODE) { }
        public PoolManagerDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "initialOwner", 1)]
        public virtual string InitialOwner { get; set; }
    }

    public partial class AllowanceFunction : AllowanceFunctionBase { }

    [Function("allowance", "uint256")]
    public class AllowanceFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("address", "spender", 2)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "id", 3)]
        public virtual BigInteger Id { get; set; }
    }

    public partial class ApproveFunction : ApproveFunctionBase { }

    [Function("approve", "bool")]
    public class ApproveFunctionBase : FunctionMessage
    {
        [Parameter("address", "spender", 1)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "id", 2)]
        public virtual BigInteger Id { get; set; }
        [Parameter("uint256", "amount", 3)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class BalanceOfFunction : BalanceOfFunctionBase { }

    [Function("balanceOf", "uint256")]
    public class BalanceOfFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("uint256", "id", 2)]
        public virtual BigInteger Id { get; set; }
    }

    public partial class BurnFunction : BurnFunctionBase { }

    [Function("burn")]
    public class BurnFunctionBase : FunctionMessage
    {
        [Parameter("address", "from", 1)]
        public virtual string From { get; set; }
        [Parameter("uint256", "id", 2)]
        public virtual BigInteger Id { get; set; }
        [Parameter("uint256", "amount", 3)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class ClearFunction : ClearFunctionBase { }

    [Function("clear")]
    public class ClearFunctionBase : FunctionMessage
    {
        [Parameter("address", "currency", 1)]
        public virtual string Currency { get; set; }
        [Parameter("uint256", "amount", 2)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class CollectProtocolFeesFunction : CollectProtocolFeesFunctionBase { }

    [Function("collectProtocolFees", "uint256")]
    public class CollectProtocolFeesFunctionBase : FunctionMessage
    {
        [Parameter("address", "recipient", 1)]
        public virtual string Recipient { get; set; }
        [Parameter("address", "currency", 2)]
        public virtual string Currency { get; set; }
        [Parameter("uint256", "amount", 3)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class DonateFunction : DonateFunctionBase { }

    [Function("donate", "int256")]
    public class DonateFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "key", 1)]
        public virtual PoolKey Key { get; set; }
        [Parameter("uint256", "amount0", 2)]
        public virtual BigInteger Amount0 { get; set; }
        [Parameter("uint256", "amount1", 3)]
        public virtual BigInteger Amount1 { get; set; }
        [Parameter("bytes", "hookData", 4)]
        public virtual byte[] HookData { get; set; }
    }

    public partial class ExtsloadFunction : ExtsloadFunctionBase { }

    [Function("extsload", "bytes32")]
    public class ExtsloadFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "slot", 1)]
        public virtual byte[] Slot { get; set; }
    }

    public partial class Extsload2Function : Extsload2FunctionBase { }

    [Function("extsload", "bytes32[]")]
    public class Extsload2FunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "startSlot", 1)]
        public virtual byte[] StartSlot { get; set; }
        [Parameter("uint256", "nSlots", 2)]
        public virtual BigInteger NSlots { get; set; }
    }

    public partial class Extsload1Function : Extsload1FunctionBase { }

    [Function("extsload", "bytes32[]")]
    public class Extsload1FunctionBase : FunctionMessage
    {
        [Parameter("bytes32[]", "slots", 1)]
        public virtual List<byte[]> Slots { get; set; }
    }

    public partial class ExttloadFunction : ExttloadFunctionBase { }

    [Function("exttload", "bytes32[]")]
    public class ExttloadFunctionBase : FunctionMessage
    {
        [Parameter("bytes32[]", "slots", 1)]
        public virtual List<byte[]> Slots { get; set; }
    }

    public partial class Exttload1Function : Exttload1FunctionBase { }

    [Function("exttload", "bytes32")]
    public class Exttload1FunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "slot", 1)]
        public virtual byte[] Slot { get; set; }
    }

    public partial class InitializeFunction : InitializeFunctionBase { }

    [Function("initialize", "int24")]
    public class InitializeFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "key", 1)]
        public virtual PoolKey Key { get; set; }
        [Parameter("uint160", "sqrtPriceX96", 2)]
        public virtual BigInteger SqrtPriceX96 { get; set; }
    }

    public partial class IsOperatorFunction : IsOperatorFunctionBase { }

    [Function("isOperator", "bool")]
    public class IsOperatorFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("address", "operator", 2)]
        public virtual string Operator { get; set; }
    }

    public partial class MintFunction : MintFunctionBase { }

    [Function("mint")]
    public class MintFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "id", 2)]
        public virtual BigInteger Id { get; set; }
        [Parameter("uint256", "amount", 3)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class ModifyLiquidityFunction : ModifyLiquidityFunctionBase { }

    [Function("modifyLiquidity", typeof(ModifyLiquidityOutputDTO))]
    public class ModifyLiquidityFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "key", 1)]
        public virtual PoolKey Key { get; set; }
        [Parameter("tuple", "params", 2)]
        public virtual ModifyLiquidityParams Params { get; set; }
        [Parameter("bytes", "hookData", 3)]
        public virtual byte[] HookData { get; set; }
    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {

    }

    public partial class ProtocolFeeControllerFunction : ProtocolFeeControllerFunctionBase { }

    [Function("protocolFeeController", "address")]
    public class ProtocolFeeControllerFunctionBase : FunctionMessage
    {

    }

    public partial class ProtocolFeesAccruedFunction : ProtocolFeesAccruedFunctionBase { }

    [Function("protocolFeesAccrued", "uint256")]
    public class ProtocolFeesAccruedFunctionBase : FunctionMessage
    {
        [Parameter("address", "currency", 1)]
        public virtual string Currency { get; set; }
    }

    public partial class SetOperatorFunction : SetOperatorFunctionBase { }

    [Function("setOperator", "bool")]
    public class SetOperatorFunctionBase : FunctionMessage
    {
        [Parameter("address", "operator", 1)]
        public virtual string Operator { get; set; }
        [Parameter("bool", "approved", 2)]
        public virtual bool Approved { get; set; }
    }

    public partial class SetProtocolFeeFunction : SetProtocolFeeFunctionBase { }

    [Function("setProtocolFee")]
    public class SetProtocolFeeFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "key", 1)]
        public virtual PoolKey Key { get; set; }
        [Parameter("uint24", "newProtocolFee", 2)]
        public virtual uint NewProtocolFee { get; set; }
    }

    public partial class SetProtocolFeeControllerFunction : SetProtocolFeeControllerFunctionBase { }

    [Function("setProtocolFeeController")]
    public class SetProtocolFeeControllerFunctionBase : FunctionMessage
    {
        [Parameter("address", "controller", 1)]
        public virtual string Controller { get; set; }
    }

    public partial class SettleFunction : SettleFunctionBase { }

    [Function("settle", "uint256")]
    public class SettleFunctionBase : FunctionMessage
    {

    }

    public partial class SettleForFunction : SettleForFunctionBase { }

    [Function("settleFor", "uint256")]
    public class SettleForFunctionBase : FunctionMessage
    {
        [Parameter("address", "recipient", 1)]
        public virtual string Recipient { get; set; }
    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceId", 1)]
        public virtual byte[] InterfaceId { get; set; }
    }

    public partial class SwapFunction : SwapFunctionBase { }

    [Function("swap", "int256")]
    public class SwapFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "key", 1)]
        public virtual PoolKey Key { get; set; }
        [Parameter("tuple", "params", 2)]
        public virtual SwapParams Params { get; set; }
        [Parameter("bytes", "hookData", 3)]
        public virtual byte[] HookData { get; set; }
    }

    public partial class SyncFunction : SyncFunctionBase { }

    [Function("sync")]
    public class SyncFunctionBase : FunctionMessage
    {
        [Parameter("address", "currency", 1)]
        public virtual string Currency { get; set; }
    }

    public partial class TakeFunction : TakeFunctionBase { }

    [Function("take")]
    public class TakeFunctionBase : FunctionMessage
    {
        [Parameter("address", "currency", 1)]
        public virtual string Currency { get; set; }
        [Parameter("address", "to", 2)]
        public virtual string To { get; set; }
        [Parameter("uint256", "amount", 3)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class TransferFunction : TransferFunctionBase { }

    [Function("transfer", "bool")]
    public class TransferFunctionBase : FunctionMessage
    {
        [Parameter("address", "receiver", 1)]
        public virtual string Receiver { get; set; }
        [Parameter("uint256", "id", 2)]
        public virtual BigInteger Id { get; set; }
        [Parameter("uint256", "amount", 3)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class TransferFromFunction : TransferFromFunctionBase { }

    [Function("transferFrom", "bool")]
    public class TransferFromFunctionBase : FunctionMessage
    {
        [Parameter("address", "sender", 1)]
        public virtual string Sender { get; set; }
        [Parameter("address", "receiver", 2)]
        public virtual string Receiver { get; set; }
        [Parameter("uint256", "id", 3)]
        public virtual BigInteger Id { get; set; }
        [Parameter("uint256", "amount", 4)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class TransferOwnershipFunction : TransferOwnershipFunctionBase { }

    [Function("transferOwnership")]
    public class TransferOwnershipFunctionBase : FunctionMessage
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner { get; set; }
    }

    public partial class UnlockFunction : UnlockFunctionBase { }

    [Function("unlock", "bytes")]
    public class UnlockFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "data", 1)]
        public virtual byte[] Data { get; set; }
    }

    public partial class UpdateDynamicLPFeeFunction : UpdateDynamicLPFeeFunctionBase { }

    [Function("updateDynamicLPFee")]
    public class UpdateDynamicLPFeeFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "key", 1)]
        public virtual PoolKey Key { get; set; }
        [Parameter("uint24", "newDynamicLPFee", 2)]
        public virtual uint NewDynamicLPFee { get; set; }
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
        [Parameter("uint256", "amount", 4, false )]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class DonateEventDTO : DonateEventDTOBase { }

    [Event("Donate")]
    public class DonateEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "id", 1, true )]
        public virtual byte[] Id { get; set; }
        [Parameter("address", "sender", 2, true )]
        public virtual string Sender { get; set; }
        [Parameter("uint256", "amount0", 3, false )]
        public virtual BigInteger Amount0 { get; set; }
        [Parameter("uint256", "amount1", 4, false )]
        public virtual BigInteger Amount1 { get; set; }
    }

    public partial class InitializeEventDTO : InitializeEventDTOBase { }

    [Event("Initialize")]
    public class InitializeEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "id", 1, true )]
        public virtual byte[] Id { get; set; }
        [Parameter("address", "currency0", 2, true )]
        public virtual string Currency0 { get; set; }
        [Parameter("address", "currency1", 3, true )]
        public virtual string Currency1 { get; set; }
        [Parameter("uint24", "fee", 4, false )]
        public virtual uint Fee { get; set; }
        [Parameter("int24", "tickSpacing", 5, false )]
        public virtual int TickSpacing { get; set; }
        [Parameter("address", "hooks", 6, false )]
        public virtual string Hooks { get; set; }
        [Parameter("uint160", "sqrtPriceX96", 7, false )]
        public virtual BigInteger SqrtPriceX96 { get; set; }
        [Parameter("int24", "tick", 8, false )]
        public virtual int Tick { get; set; }
    }

    public partial class ModifyLiquidityEventDTO : ModifyLiquidityEventDTOBase { }

    [Event("ModifyLiquidity")]
    public class ModifyLiquidityEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "id", 1, true )]
        public virtual byte[] Id { get; set; }
        [Parameter("address", "sender", 2, true )]
        public virtual string Sender { get; set; }
        [Parameter("int24", "tickLower", 3, false )]
        public virtual int TickLower { get; set; }
        [Parameter("int24", "tickUpper", 4, false )]
        public virtual int TickUpper { get; set; }
        [Parameter("int256", "liquidityDelta", 5, false )]
        public virtual BigInteger LiquidityDelta { get; set; }
        [Parameter("bytes32", "salt", 6, false )]
        public virtual byte[] Salt { get; set; }
    }

    public partial class OperatorSetEventDTO : OperatorSetEventDTOBase { }

    [Event("OperatorSet")]
    public class OperatorSetEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true )]
        public virtual string Owner { get; set; }
        [Parameter("address", "operator", 2, true )]
        public virtual string Operator { get; set; }
        [Parameter("bool", "approved", 3, false )]
        public virtual bool Approved { get; set; }
    }

    public partial class OwnershipTransferredEventDTO : OwnershipTransferredEventDTOBase { }

    [Event("OwnershipTransferred")]
    public class OwnershipTransferredEventDTOBase : IEventDTO
    {
        [Parameter("address", "user", 1, true )]
        public virtual string User { get; set; }
        [Parameter("address", "newOwner", 2, true )]
        public virtual string NewOwner { get; set; }
    }

    public partial class ProtocolFeeControllerUpdatedEventDTO : ProtocolFeeControllerUpdatedEventDTOBase { }

    [Event("ProtocolFeeControllerUpdated")]
    public class ProtocolFeeControllerUpdatedEventDTOBase : IEventDTO
    {
        [Parameter("address", "protocolFeeController", 1, true )]
        public virtual string ProtocolFeeController { get; set; }
    }

    public partial class ProtocolFeeUpdatedEventDTO : ProtocolFeeUpdatedEventDTOBase { }

    [Event("ProtocolFeeUpdated")]
    public class ProtocolFeeUpdatedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "id", 1, true )]
        public virtual byte[] Id { get; set; }
        [Parameter("uint24", "protocolFee", 2, false )]
        public virtual uint ProtocolFee { get; set; }
    }

    public partial class SwapEventDTO : SwapEventDTOBase { }

    [Event("Swap")]
    public class SwapEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "id", 1, true )]
        public virtual byte[] Id { get; set; }
        [Parameter("address", "sender", 2, true )]
        public virtual string Sender { get; set; }
        [Parameter("int128", "amount0", 3, false )]
        public virtual BigInteger Amount0 { get; set; }
        [Parameter("int128", "amount1", 4, false )]
        public virtual BigInteger Amount1 { get; set; }
        [Parameter("uint160", "sqrtPriceX96", 5, false )]
        public virtual BigInteger SqrtPriceX96 { get; set; }
        [Parameter("uint128", "liquidity", 6, false )]
        public virtual BigInteger Liquidity { get; set; }
        [Parameter("int24", "tick", 7, false )]
        public virtual int Tick { get; set; }
        [Parameter("uint24", "fee", 8, false )]
        public virtual uint Fee { get; set; }
    }

    public partial class TransferEventDTO : TransferEventDTOBase { }

    [Event("Transfer")]
    public class TransferEventDTOBase : IEventDTO
    {
        [Parameter("address", "caller", 1, false )]
        public virtual string Caller { get; set; }
        [Parameter("address", "from", 2, true )]
        public virtual string From { get; set; }
        [Parameter("address", "to", 3, true )]
        public virtual string To { get; set; }
        [Parameter("uint256", "id", 4, true )]
        public virtual BigInteger Id { get; set; }
        [Parameter("uint256", "amount", 5, false )]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class AlreadyUnlockedError : AlreadyUnlockedErrorBase { }
    [Error("AlreadyUnlocked")]
    public class AlreadyUnlockedErrorBase : IErrorDTO
    {
    }

    public partial class CurrenciesOutOfOrderOrEqualError : CurrenciesOutOfOrderOrEqualErrorBase { }

    [Error("CurrenciesOutOfOrderOrEqual")]
    public class CurrenciesOutOfOrderOrEqualErrorBase : IErrorDTO
    {
        [Parameter("address", "currency0", 1)]
        public virtual string Currency0 { get; set; }
        [Parameter("address", "currency1", 2)]
        public virtual string Currency1 { get; set; }
    }

    public partial class CurrencyNotSettledError : CurrencyNotSettledErrorBase { }
    [Error("CurrencyNotSettled")]
    public class CurrencyNotSettledErrorBase : IErrorDTO
    {
    }

    public partial class DelegateCallNotAllowedError : DelegateCallNotAllowedErrorBase { }
    [Error("DelegateCallNotAllowed")]
    public class DelegateCallNotAllowedErrorBase : IErrorDTO
    {
    }

    public partial class InvalidCallerError : InvalidCallerErrorBase { }
    [Error("InvalidCaller")]
    public class InvalidCallerErrorBase : IErrorDTO
    {
    }

    public partial class ManagerLockedError : ManagerLockedErrorBase { }
    [Error("ManagerLocked")]
    public class ManagerLockedErrorBase : IErrorDTO
    {
    }

    public partial class MustClearExactPositiveDeltaError : MustClearExactPositiveDeltaErrorBase { }
    [Error("MustClearExactPositiveDelta")]
    public class MustClearExactPositiveDeltaErrorBase : IErrorDTO
    {
    }

    public partial class NonzeroNativeValueError : NonzeroNativeValueErrorBase { }
    [Error("NonzeroNativeValue")]
    public class NonzeroNativeValueErrorBase : IErrorDTO
    {
    }

    public partial class PoolNotInitializedError : PoolNotInitializedErrorBase { }
    [Error("PoolNotInitialized")]
    public class PoolNotInitializedErrorBase : IErrorDTO
    {
    }

    public partial class ProtocolFeeCurrencySyncedError : ProtocolFeeCurrencySyncedErrorBase { }
    [Error("ProtocolFeeCurrencySynced")]
    public class ProtocolFeeCurrencySyncedErrorBase : IErrorDTO
    {
    }

    public partial class ProtocolFeeTooLargeError : ProtocolFeeTooLargeErrorBase { }

    [Error("ProtocolFeeTooLarge")]
    public class ProtocolFeeTooLargeErrorBase : IErrorDTO
    {
        [Parameter("uint24", "fee", 1)]
        public virtual uint Fee { get; set; }
    }

    public partial class SwapAmountCannotBeZeroError : SwapAmountCannotBeZeroErrorBase { }
    [Error("SwapAmountCannotBeZero")]
    public class SwapAmountCannotBeZeroErrorBase : IErrorDTO
    {
    }

    public partial class TickSpacingTooLargeError : TickSpacingTooLargeErrorBase { }

    [Error("TickSpacingTooLarge")]
    public class TickSpacingTooLargeErrorBase : IErrorDTO
    {
        [Parameter("int24", "tickSpacing", 1)]
        public virtual int TickSpacing { get; set; }
    }

    public partial class TickSpacingTooSmallError : TickSpacingTooSmallErrorBase { }

    [Error("TickSpacingTooSmall")]
    public class TickSpacingTooSmallErrorBase : IErrorDTO
    {
        [Parameter("int24", "tickSpacing", 1)]
        public virtual int TickSpacing { get; set; }
    }

    public partial class UnauthorizedDynamicLPFeeUpdateError : UnauthorizedDynamicLPFeeUpdateErrorBase { }
    [Error("UnauthorizedDynamicLPFeeUpdate")]
    public class UnauthorizedDynamicLPFeeUpdateErrorBase : IErrorDTO
    {
    }

    public partial class AllowanceOutputDTO : AllowanceOutputDTOBase { }

    [FunctionOutput]
    public class AllowanceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "amount", 1)]
        public virtual BigInteger Amount { get; set; }
    }



    public partial class BalanceOfOutputDTO : BalanceOfOutputDTOBase { }

    [FunctionOutput]
    public class BalanceOfOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "balance", 1)]
        public virtual BigInteger Balance { get; set; }
    }









    public partial class ExtsloadOutputDTO : ExtsloadOutputDTOBase { }

    [FunctionOutput]
    public class ExtsloadOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class Extsload2OutputDTO : Extsload2OutputDTOBase { }

    [FunctionOutput]
    public class Extsload2OutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32[]", "", 1)]
        public virtual List<byte[]> ReturnValue1 { get; set; }
    }

    public partial class Extsload1OutputDTO : Extsload1OutputDTOBase { }

    [FunctionOutput]
    public class Extsload1OutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32[]", "", 1)]
        public virtual List<byte[]> ReturnValue1 { get; set; }
    }

    public partial class ExttloadOutputDTO : ExttloadOutputDTOBase { }

    [FunctionOutput]
    public class ExttloadOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32[]", "", 1)]
        public virtual List<byte[]> ReturnValue1 { get; set; }
    }

    public partial class Exttload1OutputDTO : Exttload1OutputDTOBase { }

    [FunctionOutput]
    public class Exttload1OutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }



    public partial class IsOperatorOutputDTO : IsOperatorOutputDTOBase { }

    [FunctionOutput]
    public class IsOperatorOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "isOperator", 1)]
        public virtual bool IsOperator { get; set; }
    }



    public partial class ModifyLiquidityOutputDTO : ModifyLiquidityOutputDTOBase { }

    [FunctionOutput]
    public class ModifyLiquidityOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("int256", "callerDelta", 1)]
        public virtual BigInteger CallerDelta { get; set; }
        [Parameter("int256", "feesAccrued", 2)]
        public virtual BigInteger FeesAccrued { get; set; }
    }

    public partial class OwnerOutputDTO : OwnerOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class ProtocolFeeControllerOutputDTO : ProtocolFeeControllerOutputDTOBase { }

    [FunctionOutput]
    public class ProtocolFeeControllerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class ProtocolFeesAccruedOutputDTO : ProtocolFeesAccruedOutputDTOBase { }

    [FunctionOutput]
    public class ProtocolFeesAccruedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "amount", 1)]
        public virtual BigInteger Amount { get; set; }
    }











    public partial class SupportsInterfaceOutputDTO : SupportsInterfaceOutputDTOBase { }

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }
















}
