using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts;
using System.Threading;
using Nethereum.Optimism.L2StandardTokenFactory.ContractDefinition;

namespace Nethereum.Optimism.L2StandardTokenFactory
{
    public partial class L2StandardTokenFactoryService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, L2StandardTokenFactoryDeployment l2StandardTokenFactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<L2StandardTokenFactoryDeployment>().SendRequestAndWaitForReceiptAsync(l2StandardTokenFactoryDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Web3.Web3 web3, L2StandardTokenFactoryDeployment l2StandardTokenFactoryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<L2StandardTokenFactoryDeployment>().SendRequestAsync(l2StandardTokenFactoryDeployment);
        }

        public static async Task<L2StandardTokenFactoryService> DeployContractAndGetServiceAsync(Web3.Web3 web3, L2StandardTokenFactoryDeployment l2StandardTokenFactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, l2StandardTokenFactoryDeployment, cancellationTokenSource);
            return new L2StandardTokenFactoryService(web3, receipt.ContractAddress);
        }

        protected Web3.Web3 Web3 { get; }

        public ContractHandler ContractHandler { get; }

        public L2StandardTokenFactoryService(Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<string> CreateStandardL2TokenRequestAsync(CreateStandardL2TokenFunction createStandardL2TokenFunction)
        {
            return ContractHandler.SendRequestAsync(createStandardL2TokenFunction);
        }

        public Task<TransactionReceipt> CreateStandardL2TokenRequestAndWaitForReceiptAsync(CreateStandardL2TokenFunction createStandardL2TokenFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(createStandardL2TokenFunction, cancellationToken);
        }

        public Task<string> CreateStandardL2TokenRequestAsync(string l1Token, string name, string symbol)
        {
            var createStandardL2TokenFunction = new CreateStandardL2TokenFunction();
            createStandardL2TokenFunction.L1Token = l1Token;
            createStandardL2TokenFunction.Name = name;
            createStandardL2TokenFunction.Symbol = symbol;

            return ContractHandler.SendRequestAsync(createStandardL2TokenFunction);
        }

        public Task<TransactionReceipt> CreateStandardL2TokenRequestAndWaitForReceiptAsync(string l1Token, string name, string symbol, CancellationTokenSource cancellationToken = null)
        {
            var createStandardL2TokenFunction = new CreateStandardL2TokenFunction();
            createStandardL2TokenFunction.L1Token = l1Token;
            createStandardL2TokenFunction.Name = name;
            createStandardL2TokenFunction.Symbol = symbol;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(createStandardL2TokenFunction, cancellationToken);
        }
    }
}
