using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using System.Threading;
using StandardToken.MyContractName.CQS;
using StandardToken.MyContractName.DTOs;
namespace StandardToken.MyContractName.Service
{

    public class MyContractNameService
    {
    
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3 web3, MyContractNameDeployment myContractNameDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<MyContractNameDeployment>().SendRequestAndWaitForReceiptAsync(myContractNameDeployment, cancellationTokenSource);
        }
        public static Task<string> DeployContractAsync(Web3 web3, MyContractNameDeployment myContractNameDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<MyContractNameDeployment>().SendRequestAsync(myContractNameDeployment);
        }
        public static async Task<MyContractNameService> DeployContractAndGetServiceAsync(Web3 web3, MyContractNameDeployment myContractNameDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, myContractNameDeployment, cancellationTokenSource);
            return new MyContractNameService(web3, receipt.ContractAddress);
        }
    
        protected Web3 Web3{ get; }
        
        protected ContractHandler ContractHandler { get; }
        
        public MyContractNameService(Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }
    
        public Task<string> NameQueryAsync(NameFunction nameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(nameFunction, blockParameter);
        }
        public Task<string> ApproveRequestAsync(ApproveFunction approveFunction)
        {
             return ContractHandler.SendRequestAsync(approveFunction);
        }
        public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(ApproveFunction approveFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }
        public Task<BigInteger> TotalSupplyQueryAsync(TotalSupplyFunction totalSupplyFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>(totalSupplyFunction, blockParameter);
        }
        public Task<string> TransferFromRequestAsync(TransferFromFunction transferFromFunction)
        {
             return ContractHandler.SendRequestAsync(transferFromFunction);
        }
        public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(TransferFromFunction transferFromFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }
        public Task<BigInteger> BalancesQueryAsync(BalancesFunction balancesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalancesFunction, BigInteger>(balancesFunction, blockParameter);
        }
        public Task<byte> DecimalsQueryAsync(DecimalsFunction decimalsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DecimalsFunction, byte>(decimalsFunction, blockParameter);
        }
        public Task<BigInteger> AllowedQueryAsync(AllowedFunction allowedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AllowedFunction, BigInteger>(allowedFunction, blockParameter);
        }
        public Task<BigInteger> BalanceOfQueryAsync(BalanceOfFunction balanceOfFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }
        public Task<string> SymbolQueryAsync(SymbolFunction symbolFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SymbolFunction, string>(symbolFunction, blockParameter);
        }
        public Task<string> TransferRequestAsync(TransferFunction transferFunction)
        {
             return ContractHandler.SendRequestAsync(transferFunction);
        }
        public Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(TransferFunction transferFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, cancellationToken);
        }
        public Task<BigInteger> AllowanceQueryAsync(AllowanceFunction allowanceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AllowanceFunction, BigInteger>(allowanceFunction, blockParameter);
        }
    }
}
