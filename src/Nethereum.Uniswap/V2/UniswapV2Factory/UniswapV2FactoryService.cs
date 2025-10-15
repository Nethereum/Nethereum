using System.Threading.Tasks;
using System.Numerics;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.ContractHandlers;
using System.Threading;
using Nethereum.Uniswap.V2.UniswapV2Factory.ContractDefinition;

namespace Nethereum.Uniswap.V2.UniswapV2Factory
{
    public partial class UniswapV2FactoryService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, UniswapV2FactoryDeployment uniswapV2FactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<UniswapV2FactoryDeployment>().SendRequestAndWaitForReceiptAsync(uniswapV2FactoryDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Web3.Web3 web3, UniswapV2FactoryDeployment uniswapV2FactoryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<UniswapV2FactoryDeployment>().SendRequestAsync(uniswapV2FactoryDeployment);
        }

        public static async Task<UniswapV2FactoryService> DeployContractAndGetServiceAsync(Web3.Web3 web3, UniswapV2FactoryDeployment uniswapV2FactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, uniswapV2FactoryDeployment, cancellationTokenSource);
            return new UniswapV2FactoryService(web3, receipt.ContractAddress);
        }

        protected Web3.Web3 Web3{ get; }

        public ContractHandler ContractHandler { get; }

        public UniswapV2FactoryService(Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<string> AllPairsQueryAsync(AllPairsFunction allPairsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AllPairsFunction, string>(allPairsFunction, blockParameter);
        }

        
        public Task<string> AllPairsQueryAsync(BigInteger returnValue1, BlockParameter blockParameter = null)
        {
            var allPairsFunction = new AllPairsFunction();
                allPairsFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<AllPairsFunction, string>(allPairsFunction, blockParameter);
        }

        public Task<BigInteger> AllPairsLengthQueryAsync(AllPairsLengthFunction allPairsLengthFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AllPairsLengthFunction, BigInteger>(allPairsLengthFunction, blockParameter);
        }

        
        public Task<BigInteger> AllPairsLengthQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AllPairsLengthFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> CreatePairRequestAsync(CreatePairFunction createPairFunction)
        {
             return ContractHandler.SendRequestAsync(createPairFunction);
        }

        public Task<TransactionReceipt> CreatePairRequestAndWaitForReceiptAsync(CreatePairFunction createPairFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(createPairFunction, cancellationToken);
        }

        public Task<string> CreatePairRequestAsync(string tokenA, string tokenB)
        {
            var createPairFunction = new CreatePairFunction();
                createPairFunction.TokenA = tokenA;
                createPairFunction.TokenB = tokenB;
            
             return ContractHandler.SendRequestAsync(createPairFunction);
        }

        public Task<TransactionReceipt> CreatePairRequestAndWaitForReceiptAsync(string tokenA, string tokenB, CancellationTokenSource cancellationToken = null)
        {
            var createPairFunction = new CreatePairFunction();
                createPairFunction.TokenA = tokenA;
                createPairFunction.TokenB = tokenB;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(createPairFunction, cancellationToken);
        }

        public Task<string> FeeToQueryAsync(FeeToFunction feeToFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FeeToFunction, string>(feeToFunction, blockParameter);
        }

        
        public Task<string> FeeToQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FeeToFunction, string>(null, blockParameter);
        }

        public Task<string> FeeToSetterQueryAsync(FeeToSetterFunction feeToSetterFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FeeToSetterFunction, string>(feeToSetterFunction, blockParameter);
        }

        
        public Task<string> FeeToSetterQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FeeToSetterFunction, string>(null, blockParameter);
        }

        public Task<string> GetPairQueryAsync(GetPairFunction getPairFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetPairFunction, string>(getPairFunction, blockParameter);
        }

        
        public Task<string> GetPairQueryAsync(string tokenA, string tokenB, BlockParameter blockParameter = null)
        {
            var getPairFunction = new GetPairFunction();
                getPairFunction.TokenA = tokenA;
                getPairFunction.TokenB = tokenB;
            
            return ContractHandler.QueryAsync<GetPairFunction, string>(getPairFunction, blockParameter);
        }
    }
}
