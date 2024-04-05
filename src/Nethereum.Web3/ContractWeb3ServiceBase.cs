using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.Contracts;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;
using System.Threading;
using Nethereum.ABI.Model;
using System.Linq;

namespace Nethereum.Web3
{
        public abstract class ContractWeb3ServiceBase:ContractServiceBase
        {
            public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync<TDeploymentMessage>(Nethereum.Web3.IWeb3 web3, TDeploymentMessage deploymentMessage = null, CancellationTokenSource cancellationTokenSource = null)
            where TDeploymentMessage : Nethereum.Contracts.ContractDeploymentMessage, new()
            {
                if(deploymentMessage == null)
                {
                    deploymentMessage = new TDeploymentMessage();
                }
                return web3.Eth.GetContractDeploymentHandler<TDeploymentMessage>().SendRequestAndWaitForReceiptAsync(deploymentMessage, cancellationTokenSource);
            }

            public static Task<string> DeployContractAsync<TDeploymentMessage>(Nethereum.Web3.IWeb3 web3, TDeploymentMessage deploymentMessage = null)
            where TDeploymentMessage : Nethereum.Contracts.ContractDeploymentMessage, new()
            {
                if(deploymentMessage == null)
                {
                   deploymentMessage = new TDeploymentMessage();
                }

                return web3.Eth.GetContractDeploymentHandler<TDeploymentMessage>().SendRequestAsync(deploymentMessage);
            }

            public Nethereum.Web3.IWeb3 Web3 { get; }

            public ContractWeb3ServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress)
            {
                Web3 = web3;
                ContractHandler = web3.Eth.GetContractHandler(contractAddress);
            }

    }
}
