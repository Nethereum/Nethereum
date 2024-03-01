using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;
using System.Threading;

namespace Nethereum.Web3
{
        public class ContractServiceBase
        {
            public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync<TDeploymentMessage>(Nethereum.Web3.IWeb3 web3, TDeploymentMessage deploymentMessage, CancellationTokenSource cancellationTokenSource = null)
            where TDeploymentMessage : Nethereum.Contracts.ContractDeploymentMessage, new()
            {
                return web3.Eth.GetContractDeploymentHandler<TDeploymentMessage>().SendRequestAndWaitForReceiptAsync(deploymentMessage, cancellationTokenSource);
            }

            public static Task<string> DeployContractAsync<TDeploymentMessage>(Nethereum.Web3.IWeb3 web3, TDeploymentMessage deploymentMessage)
            where TDeploymentMessage : Nethereum.Contracts.ContractDeploymentMessage, new()
            {
                return web3.Eth.GetContractDeploymentHandler<TDeploymentMessage>().SendRequestAsync(deploymentMessage);
            }

            public Nethereum.Web3.IWeb3 Web3 { get; }

            public ContractHandler ContractHandler { get; }

            public ContractServiceBase(Nethereum.Web3.Web3 web3, string contractAddress)
            {
                Web3 = web3;
                ContractHandler = web3.Eth.GetContractHandler(contractAddress);
            }

            public ContractServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress)
            {
                Web3 = web3;
                ContractHandler = web3.Eth.GetContractHandler(contractAddress);
            }
        }
}
