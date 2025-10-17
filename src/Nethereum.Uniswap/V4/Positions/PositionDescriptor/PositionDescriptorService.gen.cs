using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.ContractHandlers;
using System.Threading;
using Nethereum.Uniswap.V4.Positions.PositionDescriptor.ContractDefinition;

namespace Nethereum.Uniswap.V4.Positions.PositionDescriptor
{
    public partial class PositionDescriptorService: PositionDescriptorServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(IWeb3 web3, PositionDescriptorDeployment positionDescriptorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<PositionDescriptorDeployment>().SendRequestAndWaitForReceiptAsync(positionDescriptorDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(IWeb3 web3, PositionDescriptorDeployment positionDescriptorDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<PositionDescriptorDeployment>().SendRequestAsync(positionDescriptorDeployment);
        }

        public static async Task<PositionDescriptorService> DeployContractAndGetServiceAsync(IWeb3 web3, PositionDescriptorDeployment positionDescriptorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, positionDescriptorDeployment, cancellationTokenSource);
            return new PositionDescriptorService(web3, receipt.ContractAddress);
        }

        public PositionDescriptorService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class PositionDescriptorServiceBase: ContractWeb3ServiceBase
    {

        public PositionDescriptorServiceBase(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<BigInteger> CurrencyRatioPriorityQueryAsync(CurrencyRatioPriorityFunction currencyRatioPriorityFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CurrencyRatioPriorityFunction, BigInteger>(currencyRatioPriorityFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> CurrencyRatioPriorityQueryAsync(string currency, BlockParameter blockParameter = null)
        {
            var currencyRatioPriorityFunction = new CurrencyRatioPriorityFunction();
                currencyRatioPriorityFunction.Currency = currency;
            
            return ContractHandler.QueryAsync<CurrencyRatioPriorityFunction, BigInteger>(currencyRatioPriorityFunction, blockParameter);
        }

        public Task<bool> FlipRatioQueryAsync(FlipRatioFunction flipRatioFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FlipRatioFunction, bool>(flipRatioFunction, blockParameter);
        }

        
        public virtual Task<bool> FlipRatioQueryAsync(string currency0, string currency1, BlockParameter blockParameter = null)
        {
            var flipRatioFunction = new FlipRatioFunction();
                flipRatioFunction.Currency0 = currency0;
                flipRatioFunction.Currency1 = currency1;
            
            return ContractHandler.QueryAsync<FlipRatioFunction, bool>(flipRatioFunction, blockParameter);
        }

        public Task<string> NativeCurrencyLabelQueryAsync(NativeCurrencyLabelFunction nativeCurrencyLabelFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NativeCurrencyLabelFunction, string>(nativeCurrencyLabelFunction, blockParameter);
        }

        
        public virtual Task<string> NativeCurrencyLabelQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NativeCurrencyLabelFunction, string>(null, blockParameter);
        }

        public Task<string> PoolManagerQueryAsync(PoolManagerFunction poolManagerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PoolManagerFunction, string>(poolManagerFunction, blockParameter);
        }

        
        public virtual Task<string> PoolManagerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PoolManagerFunction, string>(null, blockParameter);
        }

        public Task<string> TokenURIQueryAsync(TokenURIFunction tokenURIFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TokenURIFunction, string>(tokenURIFunction, blockParameter);
        }

        
        public virtual Task<string> TokenURIQueryAsync(string positionManager, BigInteger tokenId, BlockParameter blockParameter = null)
        {
            var tokenURIFunction = new TokenURIFunction();
                tokenURIFunction.PositionManager = positionManager;
                tokenURIFunction.TokenId = tokenId;
            
            return ContractHandler.QueryAsync<TokenURIFunction, string>(tokenURIFunction, blockParameter);
        }

        public Task<string> WrappedNativeQueryAsync(WrappedNativeFunction wrappedNativeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<WrappedNativeFunction, string>(wrappedNativeFunction, blockParameter);
        }

        
        public virtual Task<string> WrappedNativeQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<WrappedNativeFunction, string>(null, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(CurrencyRatioPriorityFunction),
                typeof(FlipRatioFunction),
                typeof(NativeCurrencyLabelFunction),
                typeof(PoolManagerFunction),
                typeof(TokenURIFunction),
                typeof(WrappedNativeFunction)
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
                typeof(InvalidAddressLengthError),
                typeof(InvalidTokenIdError),
                typeof(StringsInsufficientHexLengthError)
            };
        }
    }
}
