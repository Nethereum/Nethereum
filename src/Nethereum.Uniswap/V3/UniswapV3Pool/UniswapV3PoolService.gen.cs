using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.ContractHandlers;
using System.Threading;
using Nethereum.Uniswap.V3.UniswapV3Pool.ContractDefinition;

namespace Nethereum.Uniswap.V3.Contracts.UniswapV3Pool
{
    public partial class UniswapV3PoolService: UniswapV3PoolServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, UniswapV3PoolDeployment uniswapV3PoolDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<UniswapV3PoolDeployment>().SendRequestAndWaitForReceiptAsync(uniswapV3PoolDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, UniswapV3PoolDeployment uniswapV3PoolDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<UniswapV3PoolDeployment>().SendRequestAsync(uniswapV3PoolDeployment);
        }

        public static async Task<UniswapV3PoolService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, UniswapV3PoolDeployment uniswapV3PoolDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, uniswapV3PoolDeployment, cancellationTokenSource);
            return new UniswapV3PoolService(web3, receipt.ContractAddress);
        }

        public UniswapV3PoolService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class UniswapV3PoolServiceBase: ContractWeb3ServiceBase
    {

        public UniswapV3PoolServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> BurnRequestAsync(BurnFunction burnFunction)
        {
             return ContractHandler.SendRequestAsync(burnFunction);
        }

        public virtual Task<TransactionReceipt> BurnRequestAndWaitForReceiptAsync(BurnFunction burnFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(burnFunction, cancellationToken);
        }

        public virtual Task<string> BurnRequestAsync(int tickLower, int tickUpper, BigInteger amount)
        {
            var burnFunction = new BurnFunction();
                burnFunction.TickLower = tickLower;
                burnFunction.TickUpper = tickUpper;
                burnFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(burnFunction);
        }

        public virtual Task<TransactionReceipt> BurnRequestAndWaitForReceiptAsync(int tickLower, int tickUpper, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var burnFunction = new BurnFunction();
                burnFunction.TickLower = tickLower;
                burnFunction.TickUpper = tickUpper;
                burnFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(burnFunction, cancellationToken);
        }

        public virtual Task<string> CollectRequestAsync(CollectFunction collectFunction)
        {
             return ContractHandler.SendRequestAsync(collectFunction);
        }

        public virtual Task<TransactionReceipt> CollectRequestAndWaitForReceiptAsync(CollectFunction collectFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(collectFunction, cancellationToken);
        }

        public virtual Task<string> CollectRequestAsync(string recipient, int tickLower, int tickUpper, BigInteger amount0Requested, BigInteger amount1Requested)
        {
            var collectFunction = new CollectFunction();
                collectFunction.Recipient = recipient;
                collectFunction.TickLower = tickLower;
                collectFunction.TickUpper = tickUpper;
                collectFunction.Amount0Requested = amount0Requested;
                collectFunction.Amount1Requested = amount1Requested;
            
             return ContractHandler.SendRequestAsync(collectFunction);
        }

        public virtual Task<TransactionReceipt> CollectRequestAndWaitForReceiptAsync(string recipient, int tickLower, int tickUpper, BigInteger amount0Requested, BigInteger amount1Requested, CancellationTokenSource cancellationToken = null)
        {
            var collectFunction = new CollectFunction();
                collectFunction.Recipient = recipient;
                collectFunction.TickLower = tickLower;
                collectFunction.TickUpper = tickUpper;
                collectFunction.Amount0Requested = amount0Requested;
                collectFunction.Amount1Requested = amount1Requested;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(collectFunction, cancellationToken);
        }

        public virtual Task<string> CollectProtocolRequestAsync(CollectProtocolFunction collectProtocolFunction)
        {
             return ContractHandler.SendRequestAsync(collectProtocolFunction);
        }

        public virtual Task<TransactionReceipt> CollectProtocolRequestAndWaitForReceiptAsync(CollectProtocolFunction collectProtocolFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(collectProtocolFunction, cancellationToken);
        }

        public virtual Task<string> CollectProtocolRequestAsync(string recipient, BigInteger amount0Requested, BigInteger amount1Requested)
        {
            var collectProtocolFunction = new CollectProtocolFunction();
                collectProtocolFunction.Recipient = recipient;
                collectProtocolFunction.Amount0Requested = amount0Requested;
                collectProtocolFunction.Amount1Requested = amount1Requested;
            
             return ContractHandler.SendRequestAsync(collectProtocolFunction);
        }

        public virtual Task<TransactionReceipt> CollectProtocolRequestAndWaitForReceiptAsync(string recipient, BigInteger amount0Requested, BigInteger amount1Requested, CancellationTokenSource cancellationToken = null)
        {
            var collectProtocolFunction = new CollectProtocolFunction();
                collectProtocolFunction.Recipient = recipient;
                collectProtocolFunction.Amount0Requested = amount0Requested;
                collectProtocolFunction.Amount1Requested = amount1Requested;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(collectProtocolFunction, cancellationToken);
        }

        public Task<string> FactoryQueryAsync(FactoryFunction factoryFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FactoryFunction, string>(factoryFunction, blockParameter);
        }

        
        public virtual Task<string> FactoryQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FactoryFunction, string>(null, blockParameter);
        }

        public Task<uint> FeeQueryAsync(FeeFunction feeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FeeFunction, uint>(feeFunction, blockParameter);
        }

        
        public virtual Task<uint> FeeQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FeeFunction, uint>(null, blockParameter);
        }

        public Task<BigInteger> FeeGrowthGlobal0X128QueryAsync(FeeGrowthGlobal0X128Function feeGrowthGlobal0X128Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FeeGrowthGlobal0X128Function, BigInteger>(feeGrowthGlobal0X128Function, blockParameter);
        }

        
        public virtual Task<BigInteger> FeeGrowthGlobal0X128QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FeeGrowthGlobal0X128Function, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> FeeGrowthGlobal1X128QueryAsync(FeeGrowthGlobal1X128Function feeGrowthGlobal1X128Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FeeGrowthGlobal1X128Function, BigInteger>(feeGrowthGlobal1X128Function, blockParameter);
        }

        
        public virtual Task<BigInteger> FeeGrowthGlobal1X128QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FeeGrowthGlobal1X128Function, BigInteger>(null, blockParameter);
        }

        public virtual Task<string> FlashRequestAsync(FlashFunction flashFunction)
        {
             return ContractHandler.SendRequestAsync(flashFunction);
        }

        public virtual Task<TransactionReceipt> FlashRequestAndWaitForReceiptAsync(FlashFunction flashFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(flashFunction, cancellationToken);
        }

        public virtual Task<string> FlashRequestAsync(string recipient, BigInteger amount0, BigInteger amount1, byte[] data)
        {
            var flashFunction = new FlashFunction();
                flashFunction.Recipient = recipient;
                flashFunction.Amount0 = amount0;
                flashFunction.Amount1 = amount1;
                flashFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(flashFunction);
        }

        public virtual Task<TransactionReceipt> FlashRequestAndWaitForReceiptAsync(string recipient, BigInteger amount0, BigInteger amount1, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var flashFunction = new FlashFunction();
                flashFunction.Recipient = recipient;
                flashFunction.Amount0 = amount0;
                flashFunction.Amount1 = amount1;
                flashFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(flashFunction, cancellationToken);
        }

        public virtual Task<string> IncreaseObservationCardinalityNextRequestAsync(IncreaseObservationCardinalityNextFunction increaseObservationCardinalityNextFunction)
        {
             return ContractHandler.SendRequestAsync(increaseObservationCardinalityNextFunction);
        }

        public virtual Task<TransactionReceipt> IncreaseObservationCardinalityNextRequestAndWaitForReceiptAsync(IncreaseObservationCardinalityNextFunction increaseObservationCardinalityNextFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(increaseObservationCardinalityNextFunction, cancellationToken);
        }

        public virtual Task<string> IncreaseObservationCardinalityNextRequestAsync(ushort observationCardinalityNext)
        {
            var increaseObservationCardinalityNextFunction = new IncreaseObservationCardinalityNextFunction();
                increaseObservationCardinalityNextFunction.ObservationCardinalityNext = observationCardinalityNext;
            
             return ContractHandler.SendRequestAsync(increaseObservationCardinalityNextFunction);
        }

        public virtual Task<TransactionReceipt> IncreaseObservationCardinalityNextRequestAndWaitForReceiptAsync(ushort observationCardinalityNext, CancellationTokenSource cancellationToken = null)
        {
            var increaseObservationCardinalityNextFunction = new IncreaseObservationCardinalityNextFunction();
                increaseObservationCardinalityNextFunction.ObservationCardinalityNext = observationCardinalityNext;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(increaseObservationCardinalityNextFunction, cancellationToken);
        }

        public virtual Task<string> InitializeRequestAsync(InitializeFunction initializeFunction)
        {
             return ContractHandler.SendRequestAsync(initializeFunction);
        }

        public virtual Task<TransactionReceipt> InitializeRequestAndWaitForReceiptAsync(InitializeFunction initializeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeFunction, cancellationToken);
        }

        public virtual Task<string> InitializeRequestAsync(BigInteger sqrtPriceX96)
        {
            var initializeFunction = new InitializeFunction();
                initializeFunction.SqrtPriceX96 = sqrtPriceX96;
            
             return ContractHandler.SendRequestAsync(initializeFunction);
        }

        public virtual Task<TransactionReceipt> InitializeRequestAndWaitForReceiptAsync(BigInteger sqrtPriceX96, CancellationTokenSource cancellationToken = null)
        {
            var initializeFunction = new InitializeFunction();
                initializeFunction.SqrtPriceX96 = sqrtPriceX96;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeFunction, cancellationToken);
        }

        public Task<BigInteger> LiquidityQueryAsync(LiquidityFunction liquidityFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<LiquidityFunction, BigInteger>(liquidityFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> LiquidityQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<LiquidityFunction, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> MaxLiquidityPerTickQueryAsync(MaxLiquidityPerTickFunction maxLiquidityPerTickFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxLiquidityPerTickFunction, BigInteger>(maxLiquidityPerTickFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> MaxLiquidityPerTickQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxLiquidityPerTickFunction, BigInteger>(null, blockParameter);
        }

        public virtual Task<string> MintRequestAsync(MintFunction mintFunction)
        {
             return ContractHandler.SendRequestAsync(mintFunction);
        }

        public virtual Task<TransactionReceipt> MintRequestAndWaitForReceiptAsync(MintFunction mintFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(mintFunction, cancellationToken);
        }

        public virtual Task<string> MintRequestAsync(string recipient, int tickLower, int tickUpper, BigInteger amount, byte[] data)
        {
            var mintFunction = new MintFunction();
                mintFunction.Recipient = recipient;
                mintFunction.TickLower = tickLower;
                mintFunction.TickUpper = tickUpper;
                mintFunction.Amount = amount;
                mintFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(mintFunction);
        }

        public virtual Task<TransactionReceipt> MintRequestAndWaitForReceiptAsync(string recipient, int tickLower, int tickUpper, BigInteger amount, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var mintFunction = new MintFunction();
                mintFunction.Recipient = recipient;
                mintFunction.TickLower = tickLower;
                mintFunction.TickUpper = tickUpper;
                mintFunction.Amount = amount;
                mintFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(mintFunction, cancellationToken);
        }

        public virtual Task<ObservationsOutputDTO> ObservationsQueryAsync(ObservationsFunction observationsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<ObservationsFunction, ObservationsOutputDTO>(observationsFunction, blockParameter);
        }

        public virtual Task<ObservationsOutputDTO> ObservationsQueryAsync(BigInteger returnValue1, BlockParameter blockParameter = null)
        {
            var observationsFunction = new ObservationsFunction();
                observationsFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryDeserializingToObjectAsync<ObservationsFunction, ObservationsOutputDTO>(observationsFunction, blockParameter);
        }

        public virtual Task<ObserveOutputDTO> ObserveQueryAsync(ObserveFunction observeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<ObserveFunction, ObserveOutputDTO>(observeFunction, blockParameter);
        }

        public virtual Task<ObserveOutputDTO> ObserveQueryAsync(List<uint> secondsAgos, BlockParameter blockParameter = null)
        {
            var observeFunction = new ObserveFunction();
                observeFunction.SecondsAgos = secondsAgos;
            
            return ContractHandler.QueryDeserializingToObjectAsync<ObserveFunction, ObserveOutputDTO>(observeFunction, blockParameter);
        }

        public virtual Task<PositionsOutputDTO> PositionsQueryAsync(PositionsFunction positionsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<PositionsFunction, PositionsOutputDTO>(positionsFunction, blockParameter);
        }

        public virtual Task<PositionsOutputDTO> PositionsQueryAsync(byte[] returnValue1, BlockParameter blockParameter = null)
        {
            var positionsFunction = new PositionsFunction();
                positionsFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryDeserializingToObjectAsync<PositionsFunction, PositionsOutputDTO>(positionsFunction, blockParameter);
        }

        public virtual Task<ProtocolFeesOutputDTO> ProtocolFeesQueryAsync(ProtocolFeesFunction protocolFeesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<ProtocolFeesFunction, ProtocolFeesOutputDTO>(protocolFeesFunction, blockParameter);
        }

        public virtual Task<ProtocolFeesOutputDTO> ProtocolFeesQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<ProtocolFeesFunction, ProtocolFeesOutputDTO>(null, blockParameter);
        }

        public virtual Task<string> SetFeeProtocolRequestAsync(SetFeeProtocolFunction setFeeProtocolFunction)
        {
             return ContractHandler.SendRequestAsync(setFeeProtocolFunction);
        }

        public virtual Task<TransactionReceipt> SetFeeProtocolRequestAndWaitForReceiptAsync(SetFeeProtocolFunction setFeeProtocolFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setFeeProtocolFunction, cancellationToken);
        }

        public virtual Task<string> SetFeeProtocolRequestAsync(byte feeProtocol0, byte feeProtocol1)
        {
            var setFeeProtocolFunction = new SetFeeProtocolFunction();
                setFeeProtocolFunction.FeeProtocol0 = feeProtocol0;
                setFeeProtocolFunction.FeeProtocol1 = feeProtocol1;
            
             return ContractHandler.SendRequestAsync(setFeeProtocolFunction);
        }

        public virtual Task<TransactionReceipt> SetFeeProtocolRequestAndWaitForReceiptAsync(byte feeProtocol0, byte feeProtocol1, CancellationTokenSource cancellationToken = null)
        {
            var setFeeProtocolFunction = new SetFeeProtocolFunction();
                setFeeProtocolFunction.FeeProtocol0 = feeProtocol0;
                setFeeProtocolFunction.FeeProtocol1 = feeProtocol1;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setFeeProtocolFunction, cancellationToken);
        }

        public virtual Task<Slot0OutputDTO> Slot0QueryAsync(Slot0Function slot0Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<Slot0Function, Slot0OutputDTO>(slot0Function, blockParameter);
        }

        public virtual Task<Slot0OutputDTO> Slot0QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<Slot0Function, Slot0OutputDTO>(null, blockParameter);
        }

        public virtual Task<SnapshotCumulativesInsideOutputDTO> SnapshotCumulativesInsideQueryAsync(SnapshotCumulativesInsideFunction snapshotCumulativesInsideFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<SnapshotCumulativesInsideFunction, SnapshotCumulativesInsideOutputDTO>(snapshotCumulativesInsideFunction, blockParameter);
        }

        public virtual Task<SnapshotCumulativesInsideOutputDTO> SnapshotCumulativesInsideQueryAsync(int tickLower, int tickUpper, BlockParameter blockParameter = null)
        {
            var snapshotCumulativesInsideFunction = new SnapshotCumulativesInsideFunction();
                snapshotCumulativesInsideFunction.TickLower = tickLower;
                snapshotCumulativesInsideFunction.TickUpper = tickUpper;
            
            return ContractHandler.QueryDeserializingToObjectAsync<SnapshotCumulativesInsideFunction, SnapshotCumulativesInsideOutputDTO>(snapshotCumulativesInsideFunction, blockParameter);
        }

        public virtual Task<string> SwapRequestAsync(SwapFunction swapFunction)
        {
             return ContractHandler.SendRequestAsync(swapFunction);
        }

        public virtual Task<TransactionReceipt> SwapRequestAndWaitForReceiptAsync(SwapFunction swapFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapFunction, cancellationToken);
        }

        public virtual Task<string> SwapRequestAsync(string recipient, bool zeroForOne, BigInteger amountSpecified, BigInteger sqrtPriceLimitX96, byte[] data)
        {
            var swapFunction = new SwapFunction();
                swapFunction.Recipient = recipient;
                swapFunction.ZeroForOne = zeroForOne;
                swapFunction.AmountSpecified = amountSpecified;
                swapFunction.SqrtPriceLimitX96 = sqrtPriceLimitX96;
                swapFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(swapFunction);
        }

        public virtual Task<TransactionReceipt> SwapRequestAndWaitForReceiptAsync(string recipient, bool zeroForOne, BigInteger amountSpecified, BigInteger sqrtPriceLimitX96, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var swapFunction = new SwapFunction();
                swapFunction.Recipient = recipient;
                swapFunction.ZeroForOne = zeroForOne;
                swapFunction.AmountSpecified = amountSpecified;
                swapFunction.SqrtPriceLimitX96 = sqrtPriceLimitX96;
                swapFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapFunction, cancellationToken);
        }

        public Task<BigInteger> TickBitmapQueryAsync(TickBitmapFunction tickBitmapFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TickBitmapFunction, BigInteger>(tickBitmapFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> TickBitmapQueryAsync(short returnValue1, BlockParameter blockParameter = null)
        {
            var tickBitmapFunction = new TickBitmapFunction();
                tickBitmapFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<TickBitmapFunction, BigInteger>(tickBitmapFunction, blockParameter);
        }

        public Task<int> TickSpacingQueryAsync(TickSpacingFunction tickSpacingFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TickSpacingFunction, int>(tickSpacingFunction, blockParameter);
        }

        
        public virtual Task<int> TickSpacingQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TickSpacingFunction, int>(null, blockParameter);
        }

        public virtual Task<TicksOutputDTO> TicksQueryAsync(TicksFunction ticksFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<TicksFunction, TicksOutputDTO>(ticksFunction, blockParameter);
        }

        public virtual Task<TicksOutputDTO> TicksQueryAsync(int returnValue1, BlockParameter blockParameter = null)
        {
            var ticksFunction = new TicksFunction();
                ticksFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryDeserializingToObjectAsync<TicksFunction, TicksOutputDTO>(ticksFunction, blockParameter);
        }

        public Task<string> Token0QueryAsync(Token0Function token0Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Token0Function, string>(token0Function, blockParameter);
        }

        
        public virtual Task<string> Token0QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Token0Function, string>(null, blockParameter);
        }

        public Task<string> Token1QueryAsync(Token1Function token1Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Token1Function, string>(token1Function, blockParameter);
        }

        
        public virtual Task<string> Token1QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Token1Function, string>(null, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(BurnFunction),
                typeof(CollectFunction),
                typeof(CollectProtocolFunction),
                typeof(FactoryFunction),
                typeof(FeeFunction),
                typeof(FeeGrowthGlobal0X128Function),
                typeof(FeeGrowthGlobal1X128Function),
                typeof(FlashFunction),
                typeof(IncreaseObservationCardinalityNextFunction),
                typeof(InitializeFunction),
                typeof(LiquidityFunction),
                typeof(MaxLiquidityPerTickFunction),
                typeof(MintFunction),
                typeof(ObservationsFunction),
                typeof(ObserveFunction),
                typeof(PositionsFunction),
                typeof(ProtocolFeesFunction),
                typeof(SetFeeProtocolFunction),
                typeof(Slot0Function),
                typeof(SnapshotCumulativesInsideFunction),
                typeof(SwapFunction),
                typeof(TickBitmapFunction),
                typeof(TickSpacingFunction),
                typeof(TicksFunction),
                typeof(Token0Function),
                typeof(Token1Function)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(BurnEventDTO),
                typeof(CollectEventDTO),
                typeof(CollectProtocolEventDTO),
                typeof(FlashEventDTO),
                typeof(IncreaseObservationCardinalityNextEventDTO),
                typeof(InitializeEventDTO),
                typeof(MintEventDTO),
                typeof(SetFeeProtocolEventDTO),
                typeof(SwapEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {

            };
        }
    }
}
