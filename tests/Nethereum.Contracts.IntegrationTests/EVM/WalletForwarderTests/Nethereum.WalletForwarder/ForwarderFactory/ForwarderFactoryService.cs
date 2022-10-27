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
using Nethereum.WalletForwarder.Contracts.ForwarderFactory.ContractDefinition;

namespace Nethereum.WalletForwarder.Contracts.ForwarderFactory
{
    public partial class ForwarderFactoryService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, ForwarderFactoryDeployment forwarderFactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ForwarderFactoryDeployment>().SendRequestAndWaitForReceiptAsync(forwarderFactoryDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, ForwarderFactoryDeployment forwarderFactoryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ForwarderFactoryDeployment>().SendRequestAsync(forwarderFactoryDeployment);
        }

        public static async Task<ForwarderFactoryService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, ForwarderFactoryDeployment forwarderFactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, forwarderFactoryDeployment, cancellationTokenSource);
            return new ForwarderFactoryService(web3, receipt.ContractAddress);
        }

        protected Nethereum.Web3.Web3 Web3{ get; }

        public ContractHandler ContractHandler { get; }

        public ForwarderFactoryService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<string> CloneForwarderRequestAsync(CloneForwarderFunction cloneForwarderFunction)
        {
             return ContractHandler.SendRequestAsync(cloneForwarderFunction);
        }

        public Task<TransactionReceipt> CloneForwarderRequestAndWaitForReceiptAsync(CloneForwarderFunction cloneForwarderFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(cloneForwarderFunction, cancellationToken);
        }

        public Task<string> CloneForwarderRequestAsync(string forwarder, BigInteger salt)
        {
            var cloneForwarderFunction = new CloneForwarderFunction();
                cloneForwarderFunction.Forwarder = forwarder;
                cloneForwarderFunction.Salt = salt;
            
             return ContractHandler.SendRequestAsync(cloneForwarderFunction);
        }

        public Task<TransactionReceipt> CloneForwarderRequestAndWaitForReceiptAsync(string forwarder, BigInteger salt, CancellationTokenSource cancellationToken = null)
        {
            var cloneForwarderFunction = new CloneForwarderFunction();
                cloneForwarderFunction.Forwarder = forwarder;
                cloneForwarderFunction.Salt = salt;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(cloneForwarderFunction, cancellationToken);
        }

        public Task<string> FlushEtherRequestAsync(FlushEtherFunction flushEtherFunction)
        {
             return ContractHandler.SendRequestAsync(flushEtherFunction);
        }

        public Task<TransactionReceipt> FlushEtherRequestAndWaitForReceiptAsync(FlushEtherFunction flushEtherFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(flushEtherFunction, cancellationToken);
        }

        public Task<string> FlushEtherRequestAsync(List<string> forwarders)
        {
            var flushEtherFunction = new FlushEtherFunction();
                flushEtherFunction.Forwarders = forwarders;
            
             return ContractHandler.SendRequestAsync(flushEtherFunction);
        }

        public Task<TransactionReceipt> FlushEtherRequestAndWaitForReceiptAsync(List<string> forwarders, CancellationTokenSource cancellationToken = null)
        {
            var flushEtherFunction = new FlushEtherFunction();
                flushEtherFunction.Forwarders = forwarders;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(flushEtherFunction, cancellationToken);
        }

        public Task<string> FlushTokensRequestAsync(FlushTokensFunction flushTokensFunction)
        {
             return ContractHandler.SendRequestAsync(flushTokensFunction);
        }

        public Task<TransactionReceipt> FlushTokensRequestAndWaitForReceiptAsync(FlushTokensFunction flushTokensFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(flushTokensFunction, cancellationToken);
        }

        public Task<string> FlushTokensRequestAsync(List<string> forwarders, string tokenAddres)
        {
            var flushTokensFunction = new FlushTokensFunction();
                flushTokensFunction.Forwarders = forwarders;
                flushTokensFunction.TokenAddres = tokenAddres;
            
             return ContractHandler.SendRequestAsync(flushTokensFunction);
        }

        public Task<TransactionReceipt> FlushTokensRequestAndWaitForReceiptAsync(List<string> forwarders, string tokenAddres, CancellationTokenSource cancellationToken = null)
        {
            var flushTokensFunction = new FlushTokensFunction();
                flushTokensFunction.Forwarders = forwarders;
                flushTokensFunction.TokenAddres = tokenAddres;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(flushTokensFunction, cancellationToken);
        }
    }
}
