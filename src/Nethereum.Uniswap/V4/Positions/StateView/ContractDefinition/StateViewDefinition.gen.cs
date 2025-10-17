using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.Uniswap.V4.Positions.StateView.ContractDefinition
{


    public partial class StateViewDeployment : StateViewDeploymentBase
    {
        public StateViewDeployment() : base(BYTECODE) { }
        public StateViewDeployment(string byteCode) : base(byteCode) { }
    }

    public class StateViewDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public StateViewDeploymentBase() : base(BYTECODE) { }
        public StateViewDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_poolManager", 1)]
        public virtual string PoolManager { get; set; }
    }

    public partial class GetFeeGrowthGlobalsFunction : GetFeeGrowthGlobalsFunctionBase { }

    [Function("getFeeGrowthGlobals", typeof(GetFeeGrowthGlobalsOutputDTO))]
    public class GetFeeGrowthGlobalsFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "poolId", 1)]
        public virtual byte[] PoolId { get; set; }
    }

    public partial class GetFeeGrowthInsideFunction : GetFeeGrowthInsideFunctionBase { }

    [Function("getFeeGrowthInside", typeof(GetFeeGrowthInsideOutputDTO))]
    public class GetFeeGrowthInsideFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "poolId", 1)]
        public virtual byte[] PoolId { get; set; }
        [Parameter("int24", "tickLower", 2)]
        public virtual int TickLower { get; set; }
        [Parameter("int24", "tickUpper", 3)]
        public virtual int TickUpper { get; set; }
    }

    public partial class GetLiquidityFunction : GetLiquidityFunctionBase { }

    [Function("getLiquidity", "uint128")]
    public class GetLiquidityFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "poolId", 1)]
        public virtual byte[] PoolId { get; set; }
    }

    public partial class GetPositionInfoFunction : GetPositionInfoFunctionBase { }

    [Function("getPositionInfo", typeof(GetPositionInfoOutputDTO))]
    public class GetPositionInfoFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "poolId", 1)]
        public virtual byte[] PoolId { get; set; }
        [Parameter("bytes32", "positionId", 2)]
        public virtual byte[] PositionId { get; set; }
    }

    public partial class GetPositionInfo1Function : GetPositionInfo1FunctionBase { }

    [Function("getPositionInfo", typeof(GetPositionInfo1OutputDTO))]
    public class GetPositionInfo1FunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "poolId", 1)]
        public virtual byte[] PoolId { get; set; }
        [Parameter("address", "owner", 2)]
        public virtual string Owner { get; set; }
        [Parameter("int24", "tickLower", 3)]
        public virtual int TickLower { get; set; }
        [Parameter("int24", "tickUpper", 4)]
        public virtual int TickUpper { get; set; }
        [Parameter("bytes32", "salt", 5)]
        public virtual byte[] Salt { get; set; }
    }

    public partial class GetPositionLiquidityFunction : GetPositionLiquidityFunctionBase { }

    [Function("getPositionLiquidity", "uint128")]
    public class GetPositionLiquidityFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "poolId", 1)]
        public virtual byte[] PoolId { get; set; }
        [Parameter("bytes32", "positionId", 2)]
        public virtual byte[] PositionId { get; set; }
    }

    public partial class GetSlot0Function : GetSlot0FunctionBase { }

    [Function("getSlot0", typeof(GetSlot0OutputDTO))]
    public class GetSlot0FunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "poolId", 1)]
        public virtual byte[] PoolId { get; set; }
    }

    public partial class GetTickBitmapFunction : GetTickBitmapFunctionBase { }

    [Function("getTickBitmap", "uint256")]
    public class GetTickBitmapFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "poolId", 1)]
        public virtual byte[] PoolId { get; set; }
        [Parameter("int16", "tick", 2)]
        public virtual short Tick { get; set; }
    }

    public partial class GetTickFeeGrowthOutsideFunction : GetTickFeeGrowthOutsideFunctionBase { }

    [Function("getTickFeeGrowthOutside", typeof(GetTickFeeGrowthOutsideOutputDTO))]
    public class GetTickFeeGrowthOutsideFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "poolId", 1)]
        public virtual byte[] PoolId { get; set; }
        [Parameter("int24", "tick", 2)]
        public virtual int Tick { get; set; }
    }

    public partial class GetTickInfoFunction : GetTickInfoFunctionBase { }

    [Function("getTickInfo", typeof(GetTickInfoOutputDTO))]
    public class GetTickInfoFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "poolId", 1)]
        public virtual byte[] PoolId { get; set; }
        [Parameter("int24", "tick", 2)]
        public virtual int Tick { get; set; }
    }

    public partial class GetTickLiquidityFunction : GetTickLiquidityFunctionBase { }

    [Function("getTickLiquidity", typeof(GetTickLiquidityOutputDTO))]
    public class GetTickLiquidityFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "poolId", 1)]
        public virtual byte[] PoolId { get; set; }
        [Parameter("int24", "tick", 2)]
        public virtual int Tick { get; set; }
    }

    public partial class PoolManagerFunction : PoolManagerFunctionBase { }

    [Function("poolManager", "address")]
    public class PoolManagerFunctionBase : FunctionMessage
    {

    }

    public partial class GetFeeGrowthGlobalsOutputDTO : GetFeeGrowthGlobalsOutputDTOBase { }

    [FunctionOutput]
    public class GetFeeGrowthGlobalsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "feeGrowthGlobal0", 1)]
        public virtual BigInteger FeeGrowthGlobal0 { get; set; }
        [Parameter("uint256", "feeGrowthGlobal1", 2)]
        public virtual BigInteger FeeGrowthGlobal1 { get; set; }
    }

    public partial class GetFeeGrowthInsideOutputDTO : GetFeeGrowthInsideOutputDTOBase { }

    [FunctionOutput]
    public class GetFeeGrowthInsideOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "feeGrowthInside0X128", 1)]
        public virtual BigInteger FeeGrowthInside0X128 { get; set; }
        [Parameter("uint256", "feeGrowthInside1X128", 2)]
        public virtual BigInteger FeeGrowthInside1X128 { get; set; }
    }

    public partial class GetLiquidityOutputDTO : GetLiquidityOutputDTOBase { }

    [FunctionOutput]
    public class GetLiquidityOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint128", "liquidity", 1)]
        public virtual BigInteger Liquidity { get; set; }
    }

    public partial class GetPositionInfoOutputDTO : GetPositionInfoOutputDTOBase { }

    [FunctionOutput]
    public class GetPositionInfoOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint128", "liquidity", 1)]
        public virtual BigInteger Liquidity { get; set; }
        [Parameter("uint256", "feeGrowthInside0LastX128", 2)]
        public virtual BigInteger FeeGrowthInside0LastX128 { get; set; }
        [Parameter("uint256", "feeGrowthInside1LastX128", 3)]
        public virtual BigInteger FeeGrowthInside1LastX128 { get; set; }
    }

    public partial class GetPositionInfo1OutputDTO : GetPositionInfo1OutputDTOBase { }

    [FunctionOutput]
    public class GetPositionInfo1OutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint128", "liquidity", 1)]
        public virtual BigInteger Liquidity { get; set; }
        [Parameter("uint256", "feeGrowthInside0LastX128", 2)]
        public virtual BigInteger FeeGrowthInside0LastX128 { get; set; }
        [Parameter("uint256", "feeGrowthInside1LastX128", 3)]
        public virtual BigInteger FeeGrowthInside1LastX128 { get; set; }
    }

    public partial class GetPositionLiquidityOutputDTO : GetPositionLiquidityOutputDTOBase { }

    [FunctionOutput]
    public class GetPositionLiquidityOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint128", "liquidity", 1)]
        public virtual BigInteger Liquidity { get; set; }
    }

    public partial class GetSlot0OutputDTO : GetSlot0OutputDTOBase { }

    [FunctionOutput]
    public class GetSlot0OutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint160", "sqrtPriceX96", 1)]
        public virtual BigInteger SqrtPriceX96 { get; set; }
        [Parameter("int24", "tick", 2)]
        public virtual int Tick { get; set; }
        [Parameter("uint24", "protocolFee", 3)]
        public virtual uint ProtocolFee { get; set; }
        [Parameter("uint24", "lpFee", 4)]
        public virtual uint LpFee { get; set; }
    }

    public partial class GetTickBitmapOutputDTO : GetTickBitmapOutputDTOBase { }

    [FunctionOutput]
    public class GetTickBitmapOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "tickBitmap", 1)]
        public virtual BigInteger TickBitmap { get; set; }
    }

    public partial class GetTickFeeGrowthOutsideOutputDTO : GetTickFeeGrowthOutsideOutputDTOBase { }

    [FunctionOutput]
    public class GetTickFeeGrowthOutsideOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "feeGrowthOutside0X128", 1)]
        public virtual BigInteger FeeGrowthOutside0X128 { get; set; }
        [Parameter("uint256", "feeGrowthOutside1X128", 2)]
        public virtual BigInteger FeeGrowthOutside1X128 { get; set; }
    }

    public partial class GetTickInfoOutputDTO : GetTickInfoOutputDTOBase { }

    [FunctionOutput]
    public class GetTickInfoOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint128", "liquidityGross", 1)]
        public virtual BigInteger LiquidityGross { get; set; }
        [Parameter("int128", "liquidityNet", 2)]
        public virtual BigInteger LiquidityNet { get; set; }
        [Parameter("uint256", "feeGrowthOutside0X128", 3)]
        public virtual BigInteger FeeGrowthOutside0X128 { get; set; }
        [Parameter("uint256", "feeGrowthOutside1X128", 4)]
        public virtual BigInteger FeeGrowthOutside1X128 { get; set; }
    }

    public partial class GetTickLiquidityOutputDTO : GetTickLiquidityOutputDTOBase { }

    [FunctionOutput]
    public class GetTickLiquidityOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint128", "liquidityGross", 1)]
        public virtual BigInteger LiquidityGross { get; set; }
        [Parameter("int128", "liquidityNet", 2)]
        public virtual BigInteger LiquidityNet { get; set; }
    }

    public partial class PoolManagerOutputDTO : PoolManagerOutputDTOBase { }

    [FunctionOutput]
    public class PoolManagerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }
}
