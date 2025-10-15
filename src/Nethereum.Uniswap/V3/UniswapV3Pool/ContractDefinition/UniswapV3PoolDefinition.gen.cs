using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.Uniswap.V3.UniswapV3Pool.ContractDefinition
{


    public partial class UniswapV3PoolDeployment : UniswapV3PoolDeploymentBase
    {
        public UniswapV3PoolDeployment() : base(BYTECODE) { }
        public UniswapV3PoolDeployment(string byteCode) : base(byteCode) { }
    }

    public class UniswapV3PoolDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public UniswapV3PoolDeploymentBase() : base(BYTECODE) { }
        public UniswapV3PoolDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class BurnFunction : BurnFunctionBase { }

    [Function("burn", typeof(BurnOutputDTO))]
    public class BurnFunctionBase : FunctionMessage
    {
        [Parameter("int24", "tickLower", 1)]
        public virtual int TickLower { get; set; }
        [Parameter("int24", "tickUpper", 2)]
        public virtual int TickUpper { get; set; }
        [Parameter("uint128", "amount", 3)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class CollectFunction : CollectFunctionBase { }

    [Function("collect", typeof(CollectOutputDTO))]
    public class CollectFunctionBase : FunctionMessage
    {
        [Parameter("address", "recipient", 1)]
        public virtual string Recipient { get; set; }
        [Parameter("int24", "tickLower", 2)]
        public virtual int TickLower { get; set; }
        [Parameter("int24", "tickUpper", 3)]
        public virtual int TickUpper { get; set; }
        [Parameter("uint128", "amount0Requested", 4)]
        public virtual BigInteger Amount0Requested { get; set; }
        [Parameter("uint128", "amount1Requested", 5)]
        public virtual BigInteger Amount1Requested { get; set; }
    }

    public partial class CollectProtocolFunction : CollectProtocolFunctionBase { }

    [Function("collectProtocol", typeof(CollectProtocolOutputDTO))]
    public class CollectProtocolFunctionBase : FunctionMessage
    {
        [Parameter("address", "recipient", 1)]
        public virtual string Recipient { get; set; }
        [Parameter("uint128", "amount0Requested", 2)]
        public virtual BigInteger Amount0Requested { get; set; }
        [Parameter("uint128", "amount1Requested", 3)]
        public virtual BigInteger Amount1Requested { get; set; }
    }

    public partial class FactoryFunction : FactoryFunctionBase { }

    [Function("factory", "address")]
    public class FactoryFunctionBase : FunctionMessage
    {

    }

    public partial class FeeFunction : FeeFunctionBase { }

    [Function("fee", "uint24")]
    public class FeeFunctionBase : FunctionMessage
    {

    }

    public partial class FeeGrowthGlobal0X128Function : FeeGrowthGlobal0X128FunctionBase { }

    [Function("feeGrowthGlobal0X128", "uint256")]
    public class FeeGrowthGlobal0X128FunctionBase : FunctionMessage
    {

    }

    public partial class FeeGrowthGlobal1X128Function : FeeGrowthGlobal1X128FunctionBase { }

    [Function("feeGrowthGlobal1X128", "uint256")]
    public class FeeGrowthGlobal1X128FunctionBase : FunctionMessage
    {

    }

    public partial class FlashFunction : FlashFunctionBase { }

    [Function("flash")]
    public class FlashFunctionBase : FunctionMessage
    {
        [Parameter("address", "recipient", 1)]
        public virtual string Recipient { get; set; }
        [Parameter("uint256", "amount0", 2)]
        public virtual BigInteger Amount0 { get; set; }
        [Parameter("uint256", "amount1", 3)]
        public virtual BigInteger Amount1 { get; set; }
        [Parameter("bytes", "data", 4)]
        public virtual byte[] Data { get; set; }
    }

    public partial class IncreaseObservationCardinalityNextFunction : IncreaseObservationCardinalityNextFunctionBase { }

    [Function("increaseObservationCardinalityNext")]
    public class IncreaseObservationCardinalityNextFunctionBase : FunctionMessage
    {
        [Parameter("uint16", "observationCardinalityNext", 1)]
        public virtual ushort ObservationCardinalityNext { get; set; }
    }

    public partial class InitializeFunction : InitializeFunctionBase { }

    [Function("initialize")]
    public class InitializeFunctionBase : FunctionMessage
    {
        [Parameter("uint160", "sqrtPriceX96", 1)]
        public virtual BigInteger SqrtPriceX96 { get; set; }
    }

    public partial class LiquidityFunction : LiquidityFunctionBase { }

    [Function("liquidity", "uint128")]
    public class LiquidityFunctionBase : FunctionMessage
    {

    }

    public partial class MaxLiquidityPerTickFunction : MaxLiquidityPerTickFunctionBase { }

    [Function("maxLiquidityPerTick", "uint128")]
    public class MaxLiquidityPerTickFunctionBase : FunctionMessage
    {

    }

    public partial class MintFunction : MintFunctionBase { }

    [Function("mint", typeof(MintOutputDTO))]
    public class MintFunctionBase : FunctionMessage
    {
        [Parameter("address", "recipient", 1)]
        public virtual string Recipient { get; set; }
        [Parameter("int24", "tickLower", 2)]
        public virtual int TickLower { get; set; }
        [Parameter("int24", "tickUpper", 3)]
        public virtual int TickUpper { get; set; }
        [Parameter("uint128", "amount", 4)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("bytes", "data", 5)]
        public virtual byte[] Data { get; set; }
    }

    public partial class ObservationsFunction : ObservationsFunctionBase { }

    [Function("observations", typeof(ObservationsOutputDTO))]
    public class ObservationsFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class ObserveFunction : ObserveFunctionBase { }

    [Function("observe", typeof(ObserveOutputDTO))]
    public class ObserveFunctionBase : FunctionMessage
    {
        [Parameter("uint32[]", "secondsAgos", 1)]
        public virtual List<uint> SecondsAgos { get; set; }
    }

    public partial class PositionsFunction : PositionsFunctionBase { }

    [Function("positions", typeof(PositionsOutputDTO))]
    public class PositionsFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class ProtocolFeesFunction : ProtocolFeesFunctionBase { }

    [Function("protocolFees", typeof(ProtocolFeesOutputDTO))]
    public class ProtocolFeesFunctionBase : FunctionMessage
    {

    }

    public partial class SetFeeProtocolFunction : SetFeeProtocolFunctionBase { }

    [Function("setFeeProtocol")]
    public class SetFeeProtocolFunctionBase : FunctionMessage
    {
        [Parameter("uint8", "feeProtocol0", 1)]
        public virtual byte FeeProtocol0 { get; set; }
        [Parameter("uint8", "feeProtocol1", 2)]
        public virtual byte FeeProtocol1 { get; set; }
    }

    public partial class Slot0Function : Slot0FunctionBase { }

    [Function("slot0", typeof(Slot0OutputDTO))]
    public class Slot0FunctionBase : FunctionMessage
    {

    }

    public partial class SnapshotCumulativesInsideFunction : SnapshotCumulativesInsideFunctionBase { }

    [Function("snapshotCumulativesInside", typeof(SnapshotCumulativesInsideOutputDTO))]
    public class SnapshotCumulativesInsideFunctionBase : FunctionMessage
    {
        [Parameter("int24", "tickLower", 1)]
        public virtual int TickLower { get; set; }
        [Parameter("int24", "tickUpper", 2)]
        public virtual int TickUpper { get; set; }
    }

    public partial class SwapFunction : SwapFunctionBase { }

    [Function("swap", typeof(SwapOutputDTO))]
    public class SwapFunctionBase : FunctionMessage
    {
        [Parameter("address", "recipient", 1)]
        public virtual string Recipient { get; set; }
        [Parameter("bool", "zeroForOne", 2)]
        public virtual bool ZeroForOne { get; set; }
        [Parameter("int256", "amountSpecified", 3)]
        public virtual BigInteger AmountSpecified { get; set; }
        [Parameter("uint160", "sqrtPriceLimitX96", 4)]
        public virtual BigInteger SqrtPriceLimitX96 { get; set; }
        [Parameter("bytes", "data", 5)]
        public virtual byte[] Data { get; set; }
    }

    public partial class TickBitmapFunction : TickBitmapFunctionBase { }

    [Function("tickBitmap", "uint256")]
    public class TickBitmapFunctionBase : FunctionMessage
    {
        [Parameter("int16", "", 1)]
        public virtual short ReturnValue1 { get; set; }
    }

    public partial class TickSpacingFunction : TickSpacingFunctionBase { }

    [Function("tickSpacing", "int24")]
    public class TickSpacingFunctionBase : FunctionMessage
    {

    }

    public partial class TicksFunction : TicksFunctionBase { }

    [Function("ticks", typeof(TicksOutputDTO))]
    public class TicksFunctionBase : FunctionMessage
    {
        [Parameter("int24", "", 1)]
        public virtual int ReturnValue1 { get; set; }
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

    public partial class BurnEventDTO : BurnEventDTOBase { }

    [Event("Burn")]
    public class BurnEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true )]
        public virtual string Owner { get; set; }
        [Parameter("int24", "tickLower", 2, true )]
        public virtual int TickLower { get; set; }
        [Parameter("int24", "tickUpper", 3, true )]
        public virtual int TickUpper { get; set; }
        [Parameter("uint128", "amount", 4, false )]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint256", "amount0", 5, false )]
        public virtual BigInteger Amount0 { get; set; }
        [Parameter("uint256", "amount1", 6, false )]
        public virtual BigInteger Amount1 { get; set; }
    }

    public partial class CollectEventDTO : CollectEventDTOBase { }

    [Event("Collect")]
    public class CollectEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true )]
        public virtual string Owner { get; set; }
        [Parameter("address", "recipient", 2, false )]
        public virtual string Recipient { get; set; }
        [Parameter("int24", "tickLower", 3, true )]
        public virtual int TickLower { get; set; }
        [Parameter("int24", "tickUpper", 4, true )]
        public virtual int TickUpper { get; set; }
        [Parameter("uint128", "amount0", 5, false )]
        public virtual BigInteger Amount0 { get; set; }
        [Parameter("uint128", "amount1", 6, false )]
        public virtual BigInteger Amount1 { get; set; }
    }

    public partial class CollectProtocolEventDTO : CollectProtocolEventDTOBase { }

    [Event("CollectProtocol")]
    public class CollectProtocolEventDTOBase : IEventDTO
    {
        [Parameter("address", "sender", 1, true )]
        public virtual string Sender { get; set; }
        [Parameter("address", "recipient", 2, true )]
        public virtual string Recipient { get; set; }
        [Parameter("uint128", "amount0", 3, false )]
        public virtual BigInteger Amount0 { get; set; }
        [Parameter("uint128", "amount1", 4, false )]
        public virtual BigInteger Amount1 { get; set; }
    }

    public partial class FlashEventDTO : FlashEventDTOBase { }

    [Event("Flash")]
    public class FlashEventDTOBase : IEventDTO
    {
        [Parameter("address", "sender", 1, true )]
        public virtual string Sender { get; set; }
        [Parameter("address", "recipient", 2, true )]
        public virtual string Recipient { get; set; }
        [Parameter("uint256", "amount0", 3, false )]
        public virtual BigInteger Amount0 { get; set; }
        [Parameter("uint256", "amount1", 4, false )]
        public virtual BigInteger Amount1 { get; set; }
        [Parameter("uint256", "paid0", 5, false )]
        public virtual BigInteger Paid0 { get; set; }
        [Parameter("uint256", "paid1", 6, false )]
        public virtual BigInteger Paid1 { get; set; }
    }

    public partial class IncreaseObservationCardinalityNextEventDTO : IncreaseObservationCardinalityNextEventDTOBase { }

    [Event("IncreaseObservationCardinalityNext")]
    public class IncreaseObservationCardinalityNextEventDTOBase : IEventDTO
    {
        [Parameter("uint16", "observationCardinalityNextOld", 1, false )]
        public virtual ushort ObservationCardinalityNextOld { get; set; }
        [Parameter("uint16", "observationCardinalityNextNew", 2, false )]
        public virtual ushort ObservationCardinalityNextNew { get; set; }
    }

    public partial class InitializeEventDTO : InitializeEventDTOBase { }

    [Event("Initialize")]
    public class InitializeEventDTOBase : IEventDTO
    {
        [Parameter("uint160", "sqrtPriceX96", 1, false )]
        public virtual BigInteger SqrtPriceX96 { get; set; }
        [Parameter("int24", "tick", 2, false )]
        public virtual int Tick { get; set; }
    }

    public partial class MintEventDTO : MintEventDTOBase { }

    [Event("Mint")]
    public class MintEventDTOBase : IEventDTO
    {
        [Parameter("address", "sender", 1, false )]
        public virtual string Sender { get; set; }
        [Parameter("address", "owner", 2, true )]
        public virtual string Owner { get; set; }
        [Parameter("int24", "tickLower", 3, true )]
        public virtual int TickLower { get; set; }
        [Parameter("int24", "tickUpper", 4, true )]
        public virtual int TickUpper { get; set; }
        [Parameter("uint128", "amount", 5, false )]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint256", "amount0", 6, false )]
        public virtual BigInteger Amount0 { get; set; }
        [Parameter("uint256", "amount1", 7, false )]
        public virtual BigInteger Amount1 { get; set; }
    }

    public partial class SetFeeProtocolEventDTO : SetFeeProtocolEventDTOBase { }

    [Event("SetFeeProtocol")]
    public class SetFeeProtocolEventDTOBase : IEventDTO
    {
        [Parameter("uint8", "feeProtocol0Old", 1, false )]
        public virtual byte FeeProtocol0Old { get; set; }
        [Parameter("uint8", "feeProtocol1Old", 2, false )]
        public virtual byte FeeProtocol1Old { get; set; }
        [Parameter("uint8", "feeProtocol0New", 3, false )]
        public virtual byte FeeProtocol0New { get; set; }
        [Parameter("uint8", "feeProtocol1New", 4, false )]
        public virtual byte FeeProtocol1New { get; set; }
    }

    public partial class SwapEventDTO : SwapEventDTOBase { }

    [Event("Swap")]
    public class SwapEventDTOBase : IEventDTO
    {
        [Parameter("address", "sender", 1, true )]
        public virtual string Sender { get; set; }
        [Parameter("address", "recipient", 2, true )]
        public virtual string Recipient { get; set; }
        [Parameter("int256", "amount0", 3, false )]
        public virtual BigInteger Amount0 { get; set; }
        [Parameter("int256", "amount1", 4, false )]
        public virtual BigInteger Amount1 { get; set; }
        [Parameter("uint160", "sqrtPriceX96", 5, false )]
        public virtual BigInteger SqrtPriceX96 { get; set; }
        [Parameter("uint128", "liquidity", 6, false )]
        public virtual BigInteger Liquidity { get; set; }
        [Parameter("int24", "tick", 7, false )]
        public virtual int Tick { get; set; }
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

    public partial class CollectOutputDTO : CollectOutputDTOBase { }

    [FunctionOutput]
    public class CollectOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint128", "amount0", 1)]
        public virtual BigInteger Amount0 { get; set; }
        [Parameter("uint128", "amount1", 2)]
        public virtual BigInteger Amount1 { get; set; }
    }

    public partial class CollectProtocolOutputDTO : CollectProtocolOutputDTOBase { }

    [FunctionOutput]
    public class CollectProtocolOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint128", "amount0", 1)]
        public virtual BigInteger Amount0 { get; set; }
        [Parameter("uint128", "amount1", 2)]
        public virtual BigInteger Amount1 { get; set; }
    }

    public partial class FactoryOutputDTO : FactoryOutputDTOBase { }

    [FunctionOutput]
    public class FactoryOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class FeeOutputDTO : FeeOutputDTOBase { }

    [FunctionOutput]
    public class FeeOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint24", "", 1)]
        public virtual uint ReturnValue1 { get; set; }
    }

    public partial class FeeGrowthGlobal0X128OutputDTO : FeeGrowthGlobal0X128OutputDTOBase { }

    [FunctionOutput]
    public class FeeGrowthGlobal0X128OutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class FeeGrowthGlobal1X128OutputDTO : FeeGrowthGlobal1X128OutputDTOBase { }

    [FunctionOutput]
    public class FeeGrowthGlobal1X128OutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }







    public partial class LiquidityOutputDTO : LiquidityOutputDTOBase { }

    [FunctionOutput]
    public class LiquidityOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint128", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class MaxLiquidityPerTickOutputDTO : MaxLiquidityPerTickOutputDTOBase { }

    [FunctionOutput]
    public class MaxLiquidityPerTickOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint128", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class MintOutputDTO : MintOutputDTOBase { }

    [FunctionOutput]
    public class MintOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "amount0", 1)]
        public virtual BigInteger Amount0 { get; set; }
        [Parameter("uint256", "amount1", 2)]
        public virtual BigInteger Amount1 { get; set; }
    }

    public partial class ObservationsOutputDTO : ObservationsOutputDTOBase { }

    [FunctionOutput]
    public class ObservationsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint32", "blockTimestamp", 1)]
        public virtual uint BlockTimestamp { get; set; }
        [Parameter("int56", "tickCumulative", 2)]
        public virtual long TickCumulative { get; set; }
        [Parameter("uint160", "secondsPerLiquidityCumulativeX128", 3)]
        public virtual BigInteger SecondsPerLiquidityCumulativeX128 { get; set; }
        [Parameter("bool", "initialized", 4)]
        public virtual bool Initialized { get; set; }
    }

    public partial class ObserveOutputDTO : ObserveOutputDTOBase { }

    [FunctionOutput]
    public class ObserveOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("int56[]", "tickCumulatives", 1)]
        public virtual List<long> TickCumulatives { get; set; }
        [Parameter("uint160[]", "secondsPerLiquidityCumulativeX128s", 2)]
        public virtual List<BigInteger> SecondsPerLiquidityCumulativeX128s { get; set; }
    }

    public partial class PositionsOutputDTO : PositionsOutputDTOBase { }

    [FunctionOutput]
    public class PositionsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint128", "liquidity", 1)]
        public virtual BigInteger Liquidity { get; set; }
        [Parameter("uint256", "feeGrowthInside0LastX128", 2)]
        public virtual BigInteger FeeGrowthInside0LastX128 { get; set; }
        [Parameter("uint256", "feeGrowthInside1LastX128", 3)]
        public virtual BigInteger FeeGrowthInside1LastX128 { get; set; }
        [Parameter("uint128", "tokensOwed0", 4)]
        public virtual BigInteger TokensOwed0 { get; set; }
        [Parameter("uint128", "tokensOwed1", 5)]
        public virtual BigInteger TokensOwed1 { get; set; }
    }

    public partial class ProtocolFeesOutputDTO : ProtocolFeesOutputDTOBase { }

    [FunctionOutput]
    public class ProtocolFeesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint128", "token0", 1)]
        public virtual BigInteger Token0 { get; set; }
        [Parameter("uint128", "token1", 2)]
        public virtual BigInteger Token1 { get; set; }
    }



    public partial class Slot0OutputDTO : Slot0OutputDTOBase { }

    [FunctionOutput]
    public class Slot0OutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint160", "sqrtPriceX96", 1)]
        public virtual BigInteger SqrtPriceX96 { get; set; }
        [Parameter("int24", "tick", 2)]
        public virtual int Tick { get; set; }
        [Parameter("uint16", "observationIndex", 3)]
        public virtual ushort ObservationIndex { get; set; }
        [Parameter("uint16", "observationCardinality", 4)]
        public virtual ushort ObservationCardinality { get; set; }
        [Parameter("uint16", "observationCardinalityNext", 5)]
        public virtual ushort ObservationCardinalityNext { get; set; }
        [Parameter("uint8", "feeProtocol", 6)]
        public virtual byte FeeProtocol { get; set; }
        [Parameter("bool", "unlocked", 7)]
        public virtual bool Unlocked { get; set; }
    }

    public partial class SnapshotCumulativesInsideOutputDTO : SnapshotCumulativesInsideOutputDTOBase { }

    [FunctionOutput]
    public class SnapshotCumulativesInsideOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("int56", "tickCumulativeInside", 1)]
        public virtual long TickCumulativeInside { get; set; }
        [Parameter("uint160", "secondsPerLiquidityInsideX128", 2)]
        public virtual BigInteger SecondsPerLiquidityInsideX128 { get; set; }
        [Parameter("uint32", "secondsInside", 3)]
        public virtual uint SecondsInside { get; set; }
    }

    public partial class SwapOutputDTO : SwapOutputDTOBase { }

    [FunctionOutput]
    public class SwapOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("int256", "amount0", 1)]
        public virtual BigInteger Amount0 { get; set; }
        [Parameter("int256", "amount1", 2)]
        public virtual BigInteger Amount1 { get; set; }
    }

    public partial class TickBitmapOutputDTO : TickBitmapOutputDTOBase { }

    [FunctionOutput]
    public class TickBitmapOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class TickSpacingOutputDTO : TickSpacingOutputDTOBase { }

    [FunctionOutput]
    public class TickSpacingOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("int24", "", 1)]
        public virtual int ReturnValue1 { get; set; }
    }

    public partial class TicksOutputDTO : TicksOutputDTOBase { }

    [FunctionOutput]
    public class TicksOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint128", "liquidityGross", 1)]
        public virtual BigInteger LiquidityGross { get; set; }
        [Parameter("int128", "liquidityNet", 2)]
        public virtual BigInteger LiquidityNet { get; set; }
        [Parameter("uint256", "feeGrowthOutside0X128", 3)]
        public virtual BigInteger FeeGrowthOutside0X128 { get; set; }
        [Parameter("uint256", "feeGrowthOutside1X128", 4)]
        public virtual BigInteger FeeGrowthOutside1X128 { get; set; }
        [Parameter("int56", "tickCumulativeOutside", 5)]
        public virtual long TickCumulativeOutside { get; set; }
        [Parameter("uint160", "secondsPerLiquidityOutsideX128", 6)]
        public virtual BigInteger SecondsPerLiquidityOutsideX128 { get; set; }
        [Parameter("uint32", "secondsOutside", 7)]
        public virtual uint SecondsOutside { get; set; }
        [Parameter("bool", "initialized", 8)]
        public virtual bool Initialized { get; set; }
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
}
