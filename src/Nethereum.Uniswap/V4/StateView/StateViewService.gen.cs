using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.ContractHandlers;
using System.Threading;
using Nethereum.Uniswap.V4.StateView.ContractDefinition;

namespace Nethereum.Uniswap.V4.StateView
{
    public partial class StateViewService: StateViewServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(IWeb3 web3, StateViewDeployment stateViewDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<StateViewDeployment>().SendRequestAndWaitForReceiptAsync(stateViewDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(IWeb3 web3, StateViewDeployment stateViewDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<StateViewDeployment>().SendRequestAsync(stateViewDeployment);
        }

        public static async Task<StateViewService> DeployContractAndGetServiceAsync(IWeb3 web3, StateViewDeployment stateViewDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, stateViewDeployment, cancellationTokenSource);
            return new StateViewService(web3, receipt.ContractAddress);
        }

        public StateViewService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class StateViewServiceBase: ContractWeb3ServiceBase
    {

        public StateViewServiceBase(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<GetFeeGrowthGlobalsOutputDTO> GetFeeGrowthGlobalsQueryAsync(GetFeeGrowthGlobalsFunction getFeeGrowthGlobalsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetFeeGrowthGlobalsFunction, GetFeeGrowthGlobalsOutputDTO>(getFeeGrowthGlobalsFunction, blockParameter);
        }

        public virtual Task<GetFeeGrowthGlobalsOutputDTO> GetFeeGrowthGlobalsQueryAsync(byte[] poolId, BlockParameter blockParameter = null)
        {
            var getFeeGrowthGlobalsFunction = new GetFeeGrowthGlobalsFunction();
                getFeeGrowthGlobalsFunction.PoolId = poolId;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetFeeGrowthGlobalsFunction, GetFeeGrowthGlobalsOutputDTO>(getFeeGrowthGlobalsFunction, blockParameter);
        }

        public virtual Task<GetFeeGrowthInsideOutputDTO> GetFeeGrowthInsideQueryAsync(GetFeeGrowthInsideFunction getFeeGrowthInsideFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetFeeGrowthInsideFunction, GetFeeGrowthInsideOutputDTO>(getFeeGrowthInsideFunction, blockParameter);
        }

        public virtual Task<GetFeeGrowthInsideOutputDTO> GetFeeGrowthInsideQueryAsync(byte[] poolId, int tickLower, int tickUpper, BlockParameter blockParameter = null)
        {
            var getFeeGrowthInsideFunction = new GetFeeGrowthInsideFunction();
                getFeeGrowthInsideFunction.PoolId = poolId;
                getFeeGrowthInsideFunction.TickLower = tickLower;
                getFeeGrowthInsideFunction.TickUpper = tickUpper;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetFeeGrowthInsideFunction, GetFeeGrowthInsideOutputDTO>(getFeeGrowthInsideFunction, blockParameter);
        }

        public Task<BigInteger> GetLiquidityQueryAsync(GetLiquidityFunction getLiquidityFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetLiquidityFunction, BigInteger>(getLiquidityFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetLiquidityQueryAsync(byte[] poolId, BlockParameter blockParameter = null)
        {
            var getLiquidityFunction = new GetLiquidityFunction();
                getLiquidityFunction.PoolId = poolId;
            
            return ContractHandler.QueryAsync<GetLiquidityFunction, BigInteger>(getLiquidityFunction, blockParameter);
        }

        public virtual Task<GetPositionInfoOutputDTO> GetPositionInfoQueryAsync(GetPositionInfoFunction getPositionInfoFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetPositionInfoFunction, GetPositionInfoOutputDTO>(getPositionInfoFunction, blockParameter);
        }

        public virtual Task<GetPositionInfoOutputDTO> GetPositionInfoQueryAsync(byte[] poolId, byte[] positionId, BlockParameter blockParameter = null)
        {
            var getPositionInfoFunction = new GetPositionInfoFunction();
                getPositionInfoFunction.PoolId = poolId;
                getPositionInfoFunction.PositionId = positionId;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetPositionInfoFunction, GetPositionInfoOutputDTO>(getPositionInfoFunction, blockParameter);
        }

        public virtual Task<GetPositionInfo1OutputDTO> GetPositionInfoQueryAsync(GetPositionInfo1Function getPositionInfo1Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetPositionInfo1Function, GetPositionInfo1OutputDTO>(getPositionInfo1Function, blockParameter);
        }

        public virtual Task<GetPositionInfo1OutputDTO> GetPositionInfoQueryAsync(byte[] poolId, string owner, int tickLower, int tickUpper, byte[] salt, BlockParameter blockParameter = null)
        {
            var getPositionInfo1Function = new GetPositionInfo1Function();
                getPositionInfo1Function.PoolId = poolId;
                getPositionInfo1Function.Owner = owner;
                getPositionInfo1Function.TickLower = tickLower;
                getPositionInfo1Function.TickUpper = tickUpper;
                getPositionInfo1Function.Salt = salt;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetPositionInfo1Function, GetPositionInfo1OutputDTO>(getPositionInfo1Function, blockParameter);
        }

        public Task<BigInteger> GetPositionLiquidityQueryAsync(GetPositionLiquidityFunction getPositionLiquidityFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetPositionLiquidityFunction, BigInteger>(getPositionLiquidityFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetPositionLiquidityQueryAsync(byte[] poolId, byte[] positionId, BlockParameter blockParameter = null)
        {
            var getPositionLiquidityFunction = new GetPositionLiquidityFunction();
                getPositionLiquidityFunction.PoolId = poolId;
                getPositionLiquidityFunction.PositionId = positionId;
            
            return ContractHandler.QueryAsync<GetPositionLiquidityFunction, BigInteger>(getPositionLiquidityFunction, blockParameter);
        }

        public virtual Task<GetSlot0OutputDTO> GetSlot0QueryAsync(GetSlot0Function getSlot0Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetSlot0Function, GetSlot0OutputDTO>(getSlot0Function, blockParameter);
        }

        public virtual Task<GetSlot0OutputDTO> GetSlot0QueryAsync(byte[] poolId, BlockParameter blockParameter = null)
        {
            var getSlot0Function = new GetSlot0Function();
                getSlot0Function.PoolId = poolId;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetSlot0Function, GetSlot0OutputDTO>(getSlot0Function, blockParameter);
        }

        public Task<BigInteger> GetTickBitmapQueryAsync(GetTickBitmapFunction getTickBitmapFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetTickBitmapFunction, BigInteger>(getTickBitmapFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetTickBitmapQueryAsync(byte[] poolId, short tick, BlockParameter blockParameter = null)
        {
            var getTickBitmapFunction = new GetTickBitmapFunction();
                getTickBitmapFunction.PoolId = poolId;
                getTickBitmapFunction.Tick = tick;
            
            return ContractHandler.QueryAsync<GetTickBitmapFunction, BigInteger>(getTickBitmapFunction, blockParameter);
        }

        public virtual Task<GetTickFeeGrowthOutsideOutputDTO> GetTickFeeGrowthOutsideQueryAsync(GetTickFeeGrowthOutsideFunction getTickFeeGrowthOutsideFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetTickFeeGrowthOutsideFunction, GetTickFeeGrowthOutsideOutputDTO>(getTickFeeGrowthOutsideFunction, blockParameter);
        }

        public virtual Task<GetTickFeeGrowthOutsideOutputDTO> GetTickFeeGrowthOutsideQueryAsync(byte[] poolId, int tick, BlockParameter blockParameter = null)
        {
            var getTickFeeGrowthOutsideFunction = new GetTickFeeGrowthOutsideFunction();
                getTickFeeGrowthOutsideFunction.PoolId = poolId;
                getTickFeeGrowthOutsideFunction.Tick = tick;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetTickFeeGrowthOutsideFunction, GetTickFeeGrowthOutsideOutputDTO>(getTickFeeGrowthOutsideFunction, blockParameter);
        }

        public virtual Task<GetTickInfoOutputDTO> GetTickInfoQueryAsync(GetTickInfoFunction getTickInfoFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetTickInfoFunction, GetTickInfoOutputDTO>(getTickInfoFunction, blockParameter);
        }

        public virtual Task<GetTickInfoOutputDTO> GetTickInfoQueryAsync(byte[] poolId, int tick, BlockParameter blockParameter = null)
        {
            var getTickInfoFunction = new GetTickInfoFunction();
                getTickInfoFunction.PoolId = poolId;
                getTickInfoFunction.Tick = tick;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetTickInfoFunction, GetTickInfoOutputDTO>(getTickInfoFunction, blockParameter);
        }

        public virtual Task<GetTickLiquidityOutputDTO> GetTickLiquidityQueryAsync(GetTickLiquidityFunction getTickLiquidityFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetTickLiquidityFunction, GetTickLiquidityOutputDTO>(getTickLiquidityFunction, blockParameter);
        }

        public virtual Task<GetTickLiquidityOutputDTO> GetTickLiquidityQueryAsync(byte[] poolId, int tick, BlockParameter blockParameter = null)
        {
            var getTickLiquidityFunction = new GetTickLiquidityFunction();
                getTickLiquidityFunction.PoolId = poolId;
                getTickLiquidityFunction.Tick = tick;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetTickLiquidityFunction, GetTickLiquidityOutputDTO>(getTickLiquidityFunction, blockParameter);
        }

        public Task<string> PoolManagerQueryAsync(PoolManagerFunction poolManagerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PoolManagerFunction, string>(poolManagerFunction, blockParameter);
        }

        
        public virtual Task<string> PoolManagerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PoolManagerFunction, string>(null, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(GetFeeGrowthGlobalsFunction),
                typeof(GetFeeGrowthInsideFunction),
                typeof(GetLiquidityFunction),
                typeof(GetPositionInfoFunction),
                typeof(GetPositionInfo1Function),
                typeof(GetPositionLiquidityFunction),
                typeof(GetSlot0Function),
                typeof(GetTickBitmapFunction),
                typeof(GetTickFeeGrowthOutsideFunction),
                typeof(GetTickInfoFunction),
                typeof(GetTickLiquidityFunction),
                typeof(PoolManagerFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {

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
