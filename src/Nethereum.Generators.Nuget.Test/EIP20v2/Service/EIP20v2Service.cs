using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using System.Threading;
using Nethereum.Generators.Nuget.Test.EIP20v2.CQS;
using Nethereum.Generators.Nuget.Test.EIP20v2.DTO;
namespace Nethereum.Generators.Nuget.Test.EIP20v2.Service
{

    public class EIP20v2Service
    {
    
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, EIP20v2Deployment eIP20v2Deployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<EIP20v2Deployment>().SendRequestAndWaitForReceiptAsync(eIP20v2Deployment, cancellationTokenSource);
        }
        public static Task<string> DeployContractAsync(Web3.Web3 web3, EIP20v2Deployment eIP20v2Deployment)
        {
            return web3.Eth.GetContractDeploymentHandler<EIP20v2Deployment>().SendRequestAsync(eIP20v2Deployment);
        }
        public static async Task<EIP20v2Service> DeployContractAndGetServiceAsync(Web3.Web3 web3, EIP20v2Deployment eIP20v2Deployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, eIP20v2Deployment, cancellationTokenSource);
            return new EIP20v2Service(web3, receipt.ContractAddress);
        }
    
        protected Web3.Web3 Web3{ get; }
        
        protected ContractHandler ContractHandler { get; }
        
        public EIP20v2Service(Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }
    
        public Task<string> NameQueryAsync(NameFunction nameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(nameFunction, blockParameter);
        }
        public Task<BigInteger> TotalSupplyQueryAsync(TotalSupplyFunction totalSupplyFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>(totalSupplyFunction, blockParameter);
        }
        public Task<BigInteger> BalancesQueryAsync(BalancesFunction balancesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalancesFunction, BigInteger>(balancesFunction, blockParameter);
        }
        public Task<byte> DecimalsQueryAsync(DecimalsFunction decimalsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DecimalsFunction, byte>(decimalsFunction, blockParameter);
        }
        public Task<string> TransferRequestAsync(TransferFunction transferFunction)
        {
             return ContractHandler.SendRequestAsync(transferFunction);
        }
        public Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(TransferFunction transferFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, cancellationToken);
        }
        public Task<string> TransferFromRequestAsync(TransferFromFunction transferFromFunction)
        {
             return ContractHandler.SendRequestAsync(transferFromFunction);
        }
        public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(TransferFromFunction transferFromFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }
        public Task<BigInteger> AllowedQueryAsync(AllowedFunction allowedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AllowedFunction, BigInteger>(allowedFunction, blockParameter);
        }
        public Task<BigInteger> BalanceOfQueryAsync(BalanceOfFunction balanceOfFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }
        public Task<string> StampContractRequestAsync(StampContractFunction stampContractFunction)
        {
             return ContractHandler.SendRequestAsync(stampContractFunction);
        }
        public Task<TransactionReceipt> StampContractRequestAndWaitForReceiptAsync(StampContractFunction stampContractFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(stampContractFunction, cancellationToken);
        }
        public Task<string> SymbolQueryAsync(SymbolFunction symbolFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SymbolFunction, string>(symbolFunction, blockParameter);
        }
        public Task<string> ApproveRequestAsync(ApproveFunction approveFunction)
        {
             return ContractHandler.SendRequestAsync(approveFunction);
        }
        public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(ApproveFunction approveFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }
        public Task<BigInteger> AllowanceQueryAsync(AllowanceFunction allowanceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AllowanceFunction, BigInteger>(allowanceFunction, blockParameter);
        }
    }
}
