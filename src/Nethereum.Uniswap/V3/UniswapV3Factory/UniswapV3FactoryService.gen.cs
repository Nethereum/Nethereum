using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.ContractHandlers;
using System.Threading;
using Nethereum.Uniswap.V3.UniswapV3Factory.ContractDefinition;

namespace Nethereum.Uniswap.V3.UniswapV3Factory
{
    public partial class UniswapV3FactoryService: UniswapV3FactoryServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(IWeb3 web3, UniswapV3FactoryDeployment uniswapV3FactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<UniswapV3FactoryDeployment>().SendRequestAndWaitForReceiptAsync(uniswapV3FactoryDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(IWeb3 web3, UniswapV3FactoryDeployment uniswapV3FactoryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<UniswapV3FactoryDeployment>().SendRequestAsync(uniswapV3FactoryDeployment);
        }

        public static async Task<UniswapV3FactoryService> DeployContractAndGetServiceAsync(IWeb3 web3, UniswapV3FactoryDeployment uniswapV3FactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, uniswapV3FactoryDeployment, cancellationTokenSource);
            return new UniswapV3FactoryService(web3, receipt.ContractAddress);
        }

        public UniswapV3FactoryService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class UniswapV3FactoryServiceBase: ContractWeb3ServiceBase
    {

        public UniswapV3FactoryServiceBase(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> CreatePoolRequestAsync(CreatePoolFunction createPoolFunction)
        {
             return ContractHandler.SendRequestAsync(createPoolFunction);
        }

        public virtual Task<TransactionReceipt> CreatePoolRequestAndWaitForReceiptAsync(CreatePoolFunction createPoolFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(createPoolFunction, cancellationToken);
        }

        public virtual Task<string> CreatePoolRequestAsync(string tokenA, string tokenB, uint fee)
        {
            var createPoolFunction = new CreatePoolFunction();
                createPoolFunction.TokenA = tokenA;
                createPoolFunction.TokenB = tokenB;
                createPoolFunction.Fee = fee;
            
             return ContractHandler.SendRequestAsync(createPoolFunction);
        }

        public virtual Task<TransactionReceipt> CreatePoolRequestAndWaitForReceiptAsync(string tokenA, string tokenB, uint fee, CancellationTokenSource cancellationToken = null)
        {
            var createPoolFunction = new CreatePoolFunction();
                createPoolFunction.TokenA = tokenA;
                createPoolFunction.TokenB = tokenB;
                createPoolFunction.Fee = fee;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(createPoolFunction, cancellationToken);
        }

        public virtual Task<string> EnableFeeAmountRequestAsync(EnableFeeAmountFunction enableFeeAmountFunction)
        {
             return ContractHandler.SendRequestAsync(enableFeeAmountFunction);
        }

        public virtual Task<TransactionReceipt> EnableFeeAmountRequestAndWaitForReceiptAsync(EnableFeeAmountFunction enableFeeAmountFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(enableFeeAmountFunction, cancellationToken);
        }

        public virtual Task<string> EnableFeeAmountRequestAsync(uint fee, int tickSpacing)
        {
            var enableFeeAmountFunction = new EnableFeeAmountFunction();
                enableFeeAmountFunction.Fee = fee;
                enableFeeAmountFunction.TickSpacing = tickSpacing;
            
             return ContractHandler.SendRequestAsync(enableFeeAmountFunction);
        }

        public virtual Task<TransactionReceipt> EnableFeeAmountRequestAndWaitForReceiptAsync(uint fee, int tickSpacing, CancellationTokenSource cancellationToken = null)
        {
            var enableFeeAmountFunction = new EnableFeeAmountFunction();
                enableFeeAmountFunction.Fee = fee;
                enableFeeAmountFunction.TickSpacing = tickSpacing;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(enableFeeAmountFunction, cancellationToken);
        }

        public Task<int> FeeAmountTickSpacingQueryAsync(FeeAmountTickSpacingFunction feeAmountTickSpacingFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FeeAmountTickSpacingFunction, int>(feeAmountTickSpacingFunction, blockParameter);
        }

        
        public virtual Task<int> FeeAmountTickSpacingQueryAsync(uint returnValue1, BlockParameter blockParameter = null)
        {
            var feeAmountTickSpacingFunction = new FeeAmountTickSpacingFunction();
                feeAmountTickSpacingFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<FeeAmountTickSpacingFunction, int>(feeAmountTickSpacingFunction, blockParameter);
        }

        public Task<string> GetPoolQueryAsync(GetPoolFunction getPoolFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetPoolFunction, string>(getPoolFunction, blockParameter);
        }

        
        public virtual Task<string> GetPoolQueryAsync(string token0, string token1, uint fee, BlockParameter blockParameter = null)
        {
            var getPoolFunction = new GetPoolFunction();
                getPoolFunction.Token0 = token0;
                getPoolFunction.Token1 = token1;
                getPoolFunction.Fee = fee;
            
            return ContractHandler.QueryAsync<GetPoolFunction, string>(getPoolFunction, blockParameter);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        
        public virtual Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public virtual Task<ParametersOutputDTO> ParametersQueryAsync(ParametersFunction parametersFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<ParametersFunction, ParametersOutputDTO>(parametersFunction, blockParameter);
        }

        public virtual Task<ParametersOutputDTO> ParametersQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<ParametersFunction, ParametersOutputDTO>(null, blockParameter);
        }

        public virtual Task<string> SetOwnerRequestAsync(SetOwnerFunction setOwnerFunction)
        {
             return ContractHandler.SendRequestAsync(setOwnerFunction);
        }

        public virtual Task<TransactionReceipt> SetOwnerRequestAndWaitForReceiptAsync(SetOwnerFunction setOwnerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setOwnerFunction, cancellationToken);
        }

        public virtual Task<string> SetOwnerRequestAsync(string owner)
        {
            var setOwnerFunction = new SetOwnerFunction();
                setOwnerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAsync(setOwnerFunction);
        }

        public virtual Task<TransactionReceipt> SetOwnerRequestAndWaitForReceiptAsync(string owner, CancellationTokenSource cancellationToken = null)
        {
            var setOwnerFunction = new SetOwnerFunction();
                setOwnerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setOwnerFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(CreatePoolFunction),
                typeof(EnableFeeAmountFunction),
                typeof(FeeAmountTickSpacingFunction),
                typeof(GetPoolFunction),
                typeof(OwnerFunction),
                typeof(ParametersFunction),
                typeof(SetOwnerFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(FeeAmountEnabledEventDTO),
                typeof(OwnerChangedEventDTO),
                typeof(PoolCreatedEventDTO)
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
