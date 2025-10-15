using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.Uniswap.V2.UniswapV2Pair.ContractDefinition
{


    public partial class UniswapV2PairDeployment : UniswapV2PairDeploymentBase
    {
        public UniswapV2PairDeployment() : base(BYTECODE) { }
        public UniswapV2PairDeployment(string byteCode) : base(byteCode) { }
    }

    public class UniswapV2PairDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "";
        public UniswapV2PairDeploymentBase() : base(BYTECODE) { }
        public UniswapV2PairDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class DOMAIN_SEPARATORFunction : DOMAIN_SEPARATORFunctionBase { }

    [Function("DOMAIN_SEPARATOR", "bytes32")]
    public class DOMAIN_SEPARATORFunctionBase : FunctionMessage
    {

    }

    public partial class MINIMUM_LIQUIDITYFunction : MINIMUM_LIQUIDITYFunctionBase { }

    [Function("MINIMUM_LIQUIDITY", "uint256")]
    public class MINIMUM_LIQUIDITYFunctionBase : FunctionMessage
    {

    }

    public partial class PERMIT_TYPEHASHFunction : PERMIT_TYPEHASHFunctionBase { }

    [Function("PERMIT_TYPEHASH", "bytes32")]
    public class PERMIT_TYPEHASHFunctionBase : FunctionMessage
    {

    }

    public partial class AllowanceFunction : AllowanceFunctionBase { }

    [Function("allowance", "uint256")]
    public class AllowanceFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("address", "spender", 2)]
        public virtual string Spender { get; set; }
    }

    public partial class ApproveFunction : ApproveFunctionBase { }

    [Function("approve", "bool")]
    public class ApproveFunctionBase : FunctionMessage
    {
        [Parameter("address", "spender", 1)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
    }

    public partial class BalanceOfFunction : BalanceOfFunctionBase { }

    [Function("balanceOf", "uint256")]
    public class BalanceOfFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class BurnFunction : BurnFunctionBase { }

    [Function("burn", typeof(BurnOutputDTO))]
    public class BurnFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
    }

    public partial class DecimalsFunction : DecimalsFunctionBase { }

    [Function("decimals", "uint8")]
    public class DecimalsFunctionBase : FunctionMessage
    {

    }

    public partial class FactoryFunction : FactoryFunctionBase { }

    [Function("factory", "address")]
    public class FactoryFunctionBase : FunctionMessage
    {

    }

    public partial class GetReservesFunction : GetReservesFunctionBase { }

    [Function("getReserves", typeof(GetReservesOutputDTO))]
    public class GetReservesFunctionBase : FunctionMessage
    {

    }

    public partial class KLastFunction : KLastFunctionBase { }

    [Function("kLast", "uint256")]
    public class KLastFunctionBase : FunctionMessage
    {

    }

    public partial class MintFunction : MintFunctionBase { }

    [Function("mint", "uint256")]
    public class MintFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
    }

    public partial class NameFunction : NameFunctionBase { }

    [Function("name", "string")]
    public class NameFunctionBase : FunctionMessage
    {

    }

    public partial class NoncesFunction : NoncesFunctionBase { }

    [Function("nonces", "uint256")]
    public class NoncesFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class PermitFunction : PermitFunctionBase { }

    [Function("permit")]
    public class PermitFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("address", "spender", 2)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "value", 3)]
        public virtual BigInteger Value { get; set; }
        [Parameter("uint256", "deadline", 4)]
        public virtual BigInteger Deadline { get; set; }
        [Parameter("uint8", "v", 5)]
        public virtual byte V { get; set; }
        [Parameter("bytes32", "r", 6)]
        public virtual byte[] R { get; set; }
        [Parameter("bytes32", "s", 7)]
        public virtual byte[] S { get; set; }
    }

    public partial class Price0CumulativeLastFunction : Price0CumulativeLastFunctionBase { }

    [Function("price0CumulativeLast", "uint256")]
    public class Price0CumulativeLastFunctionBase : FunctionMessage
    {

    }

    public partial class Price1CumulativeLastFunction : Price1CumulativeLastFunctionBase { }

    [Function("price1CumulativeLast", "uint256")]
    public class Price1CumulativeLastFunctionBase : FunctionMessage
    {

    }

    public partial class SkimFunction : SkimFunctionBase { }

    [Function("skim")]
    public class SkimFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
    }

    public partial class SwapFunction : SwapFunctionBase { }

    [Function("swap")]
    public class SwapFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "amount0Out", 1)]
        public virtual BigInteger Amount0Out { get; set; }
        [Parameter("uint256", "amount1Out", 2)]
        public virtual BigInteger Amount1Out { get; set; }
        [Parameter("address", "to", 3)]
        public virtual string To { get; set; }
        [Parameter("bytes", "data", 4)]
        public virtual byte[] Data { get; set; }
    }

    public partial class SymbolFunction : SymbolFunctionBase { }

    [Function("symbol", "string")]
    public class SymbolFunctionBase : FunctionMessage
    {

    }

    public partial class SyncFunction : SyncFunctionBase { }

    [Function("sync")]
    public class SyncFunctionBase : FunctionMessage
    {

    }

    public partial class Token0Function : Token0FunctionBase { }

    [Function("token0", "address")]
    public class Token0FunctionBase : FunctionMessage
    {

    }

    public partial class Token1Function : Token1FunctionBase { }

    [Function("token1", "address")]
    public class Token1FunctionBase : FunctionMessage
    {

    }

    public partial class TotalSupplyFunction : TotalSupplyFunctionBase { }

    [Function("totalSupply", "uint256")]
    public class TotalSupplyFunctionBase : FunctionMessage
    {

    }

    public partial class TransferFunction : TransferFunctionBase { }

    [Function("transfer", "bool")]
    public class TransferFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
    }

    public partial class TransferFromFunction : TransferFromFunctionBase { }

    [Function("transferFrom", "bool")]
    public class TransferFromFunctionBase : FunctionMessage
    {
        [Parameter("address", "from", 1)]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2)]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 3)]
        public virtual BigInteger Value { get; set; }
    }

    public partial class ApprovalEventDTO : ApprovalEventDTOBase { }

    [Event("Approval")]
    public class ApprovalEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true )]
        public virtual string Owner { get; set; }
        [Parameter("address", "spender", 2, true )]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "value", 3, false )]
        public virtual BigInteger Value { get; set; }
    }

    public partial class BurnEventDTO : BurnEventDTOBase { }

    [Event("Burn")]
    public class BurnEventDTOBase : IEventDTO
    {
        [Parameter("address", "sender", 1, true )]
        public virtual string Sender { get; set; }
        [Parameter("uint256", "amount0", 2, false )]
        public virtual BigInteger Amount0 { get; set; }
        [Parameter("uint256", "amount1", 3, false )]
        public virtual BigInteger Amount1 { get; set; }
        [Parameter("address", "to", 4, true )]
        public virtual string To { get; set; }
    }

    public partial class MintEventDTO : MintEventDTOBase { }

    [Event("Mint")]
    public class MintEventDTOBase : IEventDTO
    {
        [Parameter("address", "sender", 1, true )]
        public virtual string Sender { get; set; }
        [Parameter("uint256", "amount0", 2, false )]
        public virtual BigInteger Amount0 { get; set; }
        [Parameter("uint256", "amount1", 3, false )]
        public virtual BigInteger Amount1 { get; set; }
    }

    public partial class SwapEventDTO : SwapEventDTOBase { }

    [Event("Swap")]
    public class SwapEventDTOBase : IEventDTO
    {
        [Parameter("address", "sender", 1, true )]
        public virtual string Sender { get; set; }
        [Parameter("uint256", "amount0In", 2, false )]
        public virtual BigInteger Amount0In { get; set; }
        [Parameter("uint256", "amount1In", 3, false )]
        public virtual BigInteger Amount1In { get; set; }
        [Parameter("uint256", "amount0Out", 4, false )]
        public virtual BigInteger Amount0Out { get; set; }
        [Parameter("uint256", "amount1Out", 5, false )]
        public virtual BigInteger Amount1Out { get; set; }
        [Parameter("address", "to", 6, true )]
        public virtual string To { get; set; }
    }

    public partial class SyncEventDTO : SyncEventDTOBase { }

    [Event("Sync")]
    public class SyncEventDTOBase : IEventDTO
    {
        [Parameter("uint112", "reserve0", 1, false )]
        public virtual BigInteger Reserve0 { get; set; }
        [Parameter("uint112", "reserve1", 2, false )]
        public virtual BigInteger Reserve1 { get; set; }
    }

    public partial class TransferEventDTO : TransferEventDTOBase { }

    [Event("Transfer")]
    public class TransferEventDTOBase : IEventDTO
    {
        [Parameter("address", "from", 1, true )]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2, true )]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 3, false )]
        public virtual BigInteger Value { get; set; }
    }

    public partial class DOMAIN_SEPARATOROutputDTO : DOMAIN_SEPARATOROutputDTOBase { }

    [FunctionOutput]
    public class DOMAIN_SEPARATOROutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class MINIMUM_LIQUIDITYOutputDTO : MINIMUM_LIQUIDITYOutputDTOBase { }

    [FunctionOutput]
    public class MINIMUM_LIQUIDITYOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class PERMIT_TYPEHASHOutputDTO : PERMIT_TYPEHASHOutputDTOBase { }

    [FunctionOutput]
    public class PERMIT_TYPEHASHOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class AllowanceOutputDTO : AllowanceOutputDTOBase { }

    [FunctionOutput]
    public class AllowanceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class BalanceOfOutputDTO : BalanceOfOutputDTOBase { }

    [FunctionOutput]
    public class BalanceOfOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class BurnOutputDTO : BurnOutputDTOBase { }

    [FunctionOutput]
    public class BurnOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "amount0", 1)]
        public virtual BigInteger Amount0 { get; set; }
        [Parameter("uint256", "amount1", 2)]
        public virtual BigInteger Amount1 { get; set; }
    }

    public partial class DecimalsOutputDTO : DecimalsOutputDTOBase { }

    [FunctionOutput]
    public class DecimalsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint8", "", 1)]
        public virtual byte ReturnValue1 { get; set; }
    }

    public partial class FactoryOutputDTO : FactoryOutputDTOBase { }

    [FunctionOutput]
    public class FactoryOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GetReservesOutputDTO : GetReservesOutputDTOBase { }

    [FunctionOutput]
    public class GetReservesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint112", "reserve0", 1)]
        public virtual BigInteger Reserve0 { get; set; }
        [Parameter("uint112", "reserve1", 2)]
        public virtual BigInteger Reserve1 { get; set; }
        [Parameter("uint32", "blockTimestampLast", 3)]
        public virtual uint BlockTimestampLast { get; set; }
    }

    public partial class KLastOutputDTO : KLastOutputDTOBase { }

    [FunctionOutput]
    public class KLastOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class NameOutputDTO : NameOutputDTOBase { }

    [FunctionOutput]
    public class NameOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class NoncesOutputDTO : NoncesOutputDTOBase { }

    [FunctionOutput]
    public class NoncesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class Price0CumulativeLastOutputDTO : Price0CumulativeLastOutputDTOBase { }

    [FunctionOutput]
    public class Price0CumulativeLastOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class Price1CumulativeLastOutputDTO : Price1CumulativeLastOutputDTOBase { }

    [FunctionOutput]
    public class Price1CumulativeLastOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }





    public partial class SymbolOutputDTO : SymbolOutputDTOBase { }

    [FunctionOutput]
    public class SymbolOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class Token0OutputDTO : Token0OutputDTOBase { }

    [FunctionOutput]
    public class Token0OutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class Token1OutputDTO : Token1OutputDTOBase { }

    [FunctionOutput]
    public class Token1OutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class TotalSupplyOutputDTO : TotalSupplyOutputDTOBase { }

    [FunctionOutput]
    public class TotalSupplyOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }




}
