using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.ContractHandlers;
using System.Threading;
using Nethereum.Uniswap.V4.Pricing.V4Quoter.ContractDefinition;

namespace Nethereum.Uniswap.V4.V4Quoter
{
    public partial class V4QuoterService: V4QuoterServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(IWeb3 web3, V4QuoterDeployment v4QuoterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<V4QuoterDeployment>().SendRequestAndWaitForReceiptAsync(v4QuoterDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(IWeb3 web3, V4QuoterDeployment v4QuoterDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<V4QuoterDeployment>().SendRequestAsync(v4QuoterDeployment);
        }

        public static async Task<V4QuoterService> DeployContractAndGetServiceAsync(IWeb3 web3, V4QuoterDeployment v4QuoterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, v4QuoterDeployment, cancellationTokenSource);
            return new V4QuoterService(web3, receipt.ContractAddress);
        }

        public V4QuoterService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class V4QuoterServiceBase: ContractWeb3ServiceBase
    {

        public V4QuoterServiceBase(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<QuoteExactInputOutputDTO> QuoteExactInputQueryAsync(QuoteExactInputFunction quoteExactInputFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<QuoteExactInputFunction, QuoteExactInputOutputDTO>(quoteExactInputFunction, blockParameter);
        }

        public virtual Task<QuoteExactInputOutputDTO> QuoteExactInputQueryAsync(QuoteExactParams @params, BlockParameter blockParameter = null)
        {
            var quoteExactInputFunction = new QuoteExactInputFunction();
                quoteExactInputFunction.Params = @params;
            
            return ContractHandler.QueryDeserializingToObjectAsync<QuoteExactInputFunction, QuoteExactInputOutputDTO>(quoteExactInputFunction, blockParameter);
        }

        public virtual Task<QuoteExactInputSingleOutputDTO> QuoteExactInputSingleQueryAsync(QuoteExactInputSingleFunction quoteExactInputSingleFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<QuoteExactInputSingleFunction, QuoteExactInputSingleOutputDTO>(quoteExactInputSingleFunction, blockParameter);
        }

        public virtual Task<QuoteExactInputSingleOutputDTO> QuoteExactInputSingleQueryAsync(QuoteExactSingleParams @params, BlockParameter blockParameter = null)
        {
            var quoteExactInputSingleFunction = new QuoteExactInputSingleFunction();
                quoteExactInputSingleFunction.Params = @params;
            
            return ContractHandler.QueryDeserializingToObjectAsync<QuoteExactInputSingleFunction, QuoteExactInputSingleOutputDTO>(quoteExactInputSingleFunction, blockParameter);
        }

        public virtual Task<QuoteExactOutputOutputDTO> QuoteExactOutputQueryAsync(QuoteExactOutputFunction quoteExactOutputFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<QuoteExactOutputFunction, QuoteExactOutputOutputDTO>(quoteExactOutputFunction, blockParameter);
        }

        public virtual Task<QuoteExactOutputOutputDTO> QuoteExactOutputQueryAsync(QuoteExactParams @params, BlockParameter blockParameter = null)
        {
            var quoteExactOutputFunction = new QuoteExactOutputFunction();
                quoteExactOutputFunction.Params = @params;
            
            return ContractHandler.QueryDeserializingToObjectAsync<QuoteExactOutputFunction, QuoteExactOutputOutputDTO>(quoteExactOutputFunction, blockParameter);
        }

        public virtual Task<QuoteExactOutputSingleOutputDTO> QuoteExactOutputSingleQueryAsync(QuoteExactOutputSingleFunction quoteExactOutputSingleFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<QuoteExactOutputSingleFunction, QuoteExactOutputSingleOutputDTO>(quoteExactOutputSingleFunction, blockParameter);
        }

        public virtual Task<QuoteExactOutputSingleOutputDTO> QuoteExactOutputSingleQueryAsync(QuoteExactSingleParams @params, BlockParameter blockParameter = null)
        {
            var quoteExactOutputSingleFunction = new QuoteExactOutputSingleFunction();
                quoteExactOutputSingleFunction.Params = @params;
            
            return ContractHandler.QueryDeserializingToObjectAsync<QuoteExactOutputSingleFunction, QuoteExactOutputSingleOutputDTO>(quoteExactOutputSingleFunction, blockParameter);
        }

        public virtual Task<string> UnlockCallbackRequestAsync(UnlockCallbackFunction unlockCallbackFunction)
        {
             return ContractHandler.SendRequestAsync(unlockCallbackFunction);
        }

        public virtual Task<TransactionReceipt> UnlockCallbackRequestAndWaitForReceiptAsync(UnlockCallbackFunction unlockCallbackFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unlockCallbackFunction, cancellationToken);
        }

        public virtual Task<string> UnlockCallbackRequestAsync(byte[] data)
        {
            var unlockCallbackFunction = new UnlockCallbackFunction();
                unlockCallbackFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(unlockCallbackFunction);
        }

        public virtual Task<TransactionReceipt> UnlockCallbackRequestAndWaitForReceiptAsync(byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var unlockCallbackFunction = new UnlockCallbackFunction();
                unlockCallbackFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unlockCallbackFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(QuoteExactInputFunction),
                typeof(QuoteExactInputSingleFunction),
                typeof(QuoteExactOutputFunction),
                typeof(QuoteExactOutputSingleFunction),
                typeof(UnlockCallbackFunction)
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
                typeof(NotEnoughLiquidityError),
                typeof(NotPoolManagerError),
                typeof(NotSelfError),
                typeof(QuoteSwapError),
                typeof(UnexpectedCallSuccessError),
                typeof(UnexpectedRevertBytesError)
            };
        }
    }
}
