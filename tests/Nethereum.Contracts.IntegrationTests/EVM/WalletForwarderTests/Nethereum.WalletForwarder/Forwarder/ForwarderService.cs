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
using Nethereum.WalletForwarder.Contracts.Forwarder.ContractDefinition;

namespace Nethereum.WalletForwarder.Contracts.Forwarder
{
    public partial class ForwarderService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, ForwarderDeployment forwarderDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ForwarderDeployment>().SendRequestAndWaitForReceiptAsync(forwarderDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, ForwarderDeployment forwarderDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ForwarderDeployment>().SendRequestAsync(forwarderDeployment);
        }

        public static async Task<ForwarderService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, ForwarderDeployment forwarderDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, forwarderDeployment, cancellationTokenSource);
            return new ForwarderService(web3, receipt.ContractAddress);
        }

        protected Nethereum.Web3.Web3 Web3{ get; }

        public ContractHandler ContractHandler { get; }

        public ForwarderService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<string> ChangeDestinationRequestAsync(ChangeDestinationFunction changeDestinationFunction)
        {
             return ContractHandler.SendRequestAsync(changeDestinationFunction);
        }

        public Task<TransactionReceipt> ChangeDestinationRequestAndWaitForReceiptAsync(ChangeDestinationFunction changeDestinationFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(changeDestinationFunction, cancellationToken);
        }

        public Task<string> ChangeDestinationRequestAsync(string newDestination)
        {
            var changeDestinationFunction = new ChangeDestinationFunction();
                changeDestinationFunction.NewDestination = newDestination;
            
             return ContractHandler.SendRequestAsync(changeDestinationFunction);
        }

        public Task<TransactionReceipt> ChangeDestinationRequestAndWaitForReceiptAsync(string newDestination, CancellationTokenSource cancellationToken = null)
        {
            var changeDestinationFunction = new ChangeDestinationFunction();
                changeDestinationFunction.NewDestination = newDestination;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(changeDestinationFunction, cancellationToken);
        }

        public Task<string> DestinationQueryAsync(DestinationFunction destinationFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DestinationFunction, string>(destinationFunction, blockParameter);
        }

        
        public Task<string> DestinationQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DestinationFunction, string>(null, blockParameter);
        }

        public Task<string> FlushRequestAsync(FlushFunction flushFunction)
        {
             return ContractHandler.SendRequestAsync(flushFunction);
        }

        public Task<string> FlushRequestAsync()
        {
             return ContractHandler.SendRequestAsync<FlushFunction>();
        }

        public Task<TransactionReceipt> FlushRequestAndWaitForReceiptAsync(FlushFunction flushFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(flushFunction, cancellationToken);
        }

        public Task<TransactionReceipt> FlushRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<FlushFunction>(null, cancellationToken);
        }

        public Task<string> FlushTokensRequestAsync(FlushTokensFunction flushTokensFunction)
        {
             return ContractHandler.SendRequestAsync(flushTokensFunction);
        }

        public Task<TransactionReceipt> FlushTokensRequestAndWaitForReceiptAsync(FlushTokensFunction flushTokensFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(flushTokensFunction, cancellationToken);
        }

        public Task<string> FlushTokensRequestAsync(string tokenContractAddress)
        {
            var flushTokensFunction = new FlushTokensFunction();
                flushTokensFunction.TokenContractAddress = tokenContractAddress;
            
             return ContractHandler.SendRequestAsync(flushTokensFunction);
        }

        public Task<TransactionReceipt> FlushTokensRequestAndWaitForReceiptAsync(string tokenContractAddress, CancellationTokenSource cancellationToken = null)
        {
            var flushTokensFunction = new FlushTokensFunction();
                flushTokensFunction.TokenContractAddress = tokenContractAddress;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(flushTokensFunction, cancellationToken);
        }

        public Task<string> InitRequestAsync(InitFunction initFunction)
        {
             return ContractHandler.SendRequestAsync(initFunction);
        }

        public Task<TransactionReceipt> InitRequestAndWaitForReceiptAsync(InitFunction initFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initFunction, cancellationToken);
        }

        public Task<string> InitRequestAsync(string newDestination)
        {
            var initFunction = new InitFunction();
                initFunction.NewDestination = newDestination;
            
             return ContractHandler.SendRequestAsync(initFunction);
        }

        public Task<TransactionReceipt> InitRequestAndWaitForReceiptAsync(string newDestination, CancellationTokenSource cancellationToken = null)
        {
            var initFunction = new InitFunction();
                initFunction.NewDestination = newDestination;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initFunction, cancellationToken);
        }

        public Task<string> WithdrawRequestAsync(WithdrawFunction withdrawFunction)
        {
             return ContractHandler.SendRequestAsync(withdrawFunction);
        }

        public Task<string> WithdrawRequestAsync()
        {
             return ContractHandler.SendRequestAsync<WithdrawFunction>();
        }

        public Task<TransactionReceipt> WithdrawRequestAndWaitForReceiptAsync(WithdrawFunction withdrawFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawFunction, cancellationToken);
        }

        public Task<TransactionReceipt> WithdrawRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<WithdrawFunction>(null, cancellationToken);
        }
    }
}
