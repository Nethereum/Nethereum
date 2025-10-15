using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.ContractHandlers;
using System.Threading;
using Nethereum.Uniswap.V3.QuoterV2.ContractDefinition;

namespace Nethereum.Uniswap.V3.QuoterV2
{
    public partial class QuoterV2Service: QuoterV2ServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(IWeb3 web3, QuoterV2Deployment quoterV2Deployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<QuoterV2Deployment>().SendRequestAndWaitForReceiptAsync(quoterV2Deployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(IWeb3 web3, QuoterV2Deployment quoterV2Deployment)
        {
            return web3.Eth.GetContractDeploymentHandler<QuoterV2Deployment>().SendRequestAsync(quoterV2Deployment);
        }

        public static async Task<QuoterV2Service> DeployContractAndGetServiceAsync(IWeb3 web3, QuoterV2Deployment quoterV2Deployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, quoterV2Deployment, cancellationTokenSource);
            return new QuoterV2Service(web3, receipt.ContractAddress);
        }

        public QuoterV2Service(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class QuoterV2ServiceBase: ContractWeb3ServiceBase
    {

        public QuoterV2ServiceBase(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<string> Weth9QueryAsync(Weth9Function weth9Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Weth9Function, string>(weth9Function, blockParameter);
        }

        
        public virtual Task<string> Weth9QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Weth9Function, string>(null, blockParameter);
        }

        public Task<string> FactoryQueryAsync(FactoryFunction factoryFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FactoryFunction, string>(factoryFunction, blockParameter);
        }

        
        public virtual Task<string> FactoryQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FactoryFunction, string>(null, blockParameter);
        }

        public virtual Task<QuoteExactInputOutputDTO> QuoteExactInputQueryAsync(QuoteExactInputFunction quoteExactInputFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<QuoteExactInputFunction, QuoteExactInputOutputDTO>(quoteExactInputFunction, blockParameter);
        }

        public virtual Task<QuoteExactInputOutputDTO> QuoteExactInputQueryAsync(byte[] path, BigInteger amountIn, BlockParameter blockParameter = null)
        {
            var quoteExactInputFunction = new QuoteExactInputFunction();
                quoteExactInputFunction.Path = path;
                quoteExactInputFunction.AmountIn = amountIn;
            
            return ContractHandler.QueryDeserializingToObjectAsync<QuoteExactInputFunction, QuoteExactInputOutputDTO>(quoteExactInputFunction, blockParameter);
        }

        public virtual Task<QuoteExactInputSingleOutputDTO> QuoteExactInputSingleQueryAsync(QuoteExactInputSingleFunction quoteExactInputSingleFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<QuoteExactInputSingleFunction, QuoteExactInputSingleOutputDTO>(quoteExactInputSingleFunction, blockParameter);
        }

        public virtual Task<QuoteExactInputSingleOutputDTO> QuoteExactInputSingleQueryAsync(QuoteExactInputSingleParams @params, BlockParameter blockParameter = null)
        {
            var quoteExactInputSingleFunction = new QuoteExactInputSingleFunction();
                quoteExactInputSingleFunction.Params = @params;
            
            return ContractHandler.QueryDeserializingToObjectAsync<QuoteExactInputSingleFunction, QuoteExactInputSingleOutputDTO>(quoteExactInputSingleFunction, blockParameter);
        }

        public virtual Task<QuoteExactOutputOutputDTO> QuoteExactOutputQueryAsync(QuoteExactOutputFunction quoteExactOutputFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<QuoteExactOutputFunction, QuoteExactOutputOutputDTO>(quoteExactOutputFunction, blockParameter);
        }

        public virtual Task<QuoteExactOutputOutputDTO> QuoteExactOutputQueryAsync(byte[] path, BigInteger amountOut, BlockParameter blockParameter = null)
        {
            var quoteExactOutputFunction = new QuoteExactOutputFunction();
                quoteExactOutputFunction.Path = path;
                quoteExactOutputFunction.AmountOut = amountOut;
            
            return ContractHandler.QueryDeserializingToObjectAsync<QuoteExactOutputFunction, QuoteExactOutputOutputDTO>(quoteExactOutputFunction, blockParameter);
        }

        public virtual Task<QuoteExactOutputSingleOutputDTO> QuoteExactOutputSingleQueryAsync(QuoteExactOutputSingleFunction quoteExactOutputSingleFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<QuoteExactOutputSingleFunction, QuoteExactOutputSingleOutputDTO>(quoteExactOutputSingleFunction, blockParameter);
        }

        public virtual Task<QuoteExactOutputSingleOutputDTO> QuoteExactOutputSingleQueryAsync(QuoteExactOutputSingleParams @params, BlockParameter blockParameter = null)
        {
            var quoteExactOutputSingleFunction = new QuoteExactOutputSingleFunction();
                quoteExactOutputSingleFunction.Params = @params;
            
            return ContractHandler.QueryDeserializingToObjectAsync<QuoteExactOutputSingleFunction, QuoteExactOutputSingleOutputDTO>(quoteExactOutputSingleFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(Weth9Function),
                typeof(FactoryFunction),
                typeof(QuoteExactInputFunction),
                typeof(QuoteExactInputSingleFunction),
                typeof(QuoteExactOutputFunction),
                typeof(QuoteExactOutputSingleFunction)
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
