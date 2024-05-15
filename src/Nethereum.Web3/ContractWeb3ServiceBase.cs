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
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts.Extensions;
using Nethereum.Contracts.Create2Deployment;


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

            public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync<TDeploymentMessage>(Nethereum.Web3.IWeb3 web3, TDeploymentMessage deploymentMessage = null, CancellationToken cancellationToken = default)
               where TDeploymentMessage : Nethereum.Contracts.ContractDeploymentMessage, new()
            {
                if (deploymentMessage == null)
                {
                    deploymentMessage = new TDeploymentMessage();
                }
                return web3.Eth.GetContractDeploymentHandler<TDeploymentMessage>().SendRequestAndWaitForReceiptAsync(deploymentMessage, cancellationToken);
            }

         public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync<TDeploymentMessage>(Nethereum.Web3.IWeb3 web3, TDeploymentMessage deploymentMessage = null, IEnumerable<ByteCodeLibrary> byteCodeLibraries = null, CancellationTokenSource cancellationTokenSource = null)
               where TDeploymentMessage : Nethereum.Contracts.ContractDeploymentMessage, new()
                {
                    if (deploymentMessage == null)
                    {
                        deploymentMessage = new TDeploymentMessage();
                    }

                    if(byteCodeLibraries != null)
                    {
                        deploymentMessage.LinkLibraries(byteCodeLibraries.ToArray());
                    }
                    return web3.Eth.GetContractDeploymentHandler<TDeploymentMessage>().SendRequestAndWaitForReceiptAsync(deploymentMessage, cancellationTokenSource);
                }

            public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync<TDeploymentMessage>(Nethereum.Web3.IWeb3 web3, TDeploymentMessage deploymentMessage = null, IEnumerable<ByteCodeLibrary> byteCodeLibraries = null, CancellationToken cancellationToken = default)
                  where TDeploymentMessage : Nethereum.Contracts.ContractDeploymentMessage, new()
            {
                if (deploymentMessage == null)
                {
                    deploymentMessage = new TDeploymentMessage();
                }

                if (byteCodeLibraries != null)
                {
                    deploymentMessage.LinkLibraries(byteCodeLibraries.ToArray());
                }
                return web3.Eth.GetContractDeploymentHandler<TDeploymentMessage>().SendRequestAndWaitForReceiptAsync(deploymentMessage, cancellationToken);
            }

        public static Task<string> DeployContractAsync<TDeploymentMessage>(Nethereum.Web3.IWeb3 web3, TDeploymentMessage deploymentMessage = null, IEnumerable<ByteCodeLibrary> byteCodeLibraries = null)
                where TDeploymentMessage : Nethereum.Contracts.ContractDeploymentMessage, new()
                {
                    if(deploymentMessage == null)
                    {
                       deploymentMessage = new TDeploymentMessage();
                    }

                    if(byteCodeLibraries != null)
                    {
                        deploymentMessage.LinkLibraries(byteCodeLibraries.ToArray());
                    }

                    return web3.Eth.GetContractDeploymentHandler<TDeploymentMessage>().SendRequestAsync(deploymentMessage);
                }


            public static Task<Create2ContractDeploymentTransactionResult> DeployCreate2ContractAsync<TDeploymentMessage>(Nethereum.Web3.IWeb3 web3, string deployerAddress, string salt, TDeploymentMessage deploymentMessage = null, params ByteCodeLibrary[] byteCodeLibraries)
             where TDeploymentMessage : Nethereum.Contracts.ContractDeploymentMessage, new()
            {
                if (deploymentMessage == null)
                {
                    deploymentMessage = new TDeploymentMessage();
                }
                
                if(byteCodeLibraries != null)
                {
                    deploymentMessage.LinkLibraries(byteCodeLibraries);
                }

                return web3.Eth.Create2DeterministicDeploymentProxyService.DeployContractRequestAsync(deploymentMessage, deployerAddress, salt, byteCodeLibraries);
            }

            public static Task<Create2ContractDeploymentTransactionReceiptResult> DeployCreate2ContractAndWaitForReceiptAsync<TDeploymentMessage>(Nethereum.Web3.IWeb3 web3, string deployerAddress, string salt, TDeploymentMessage deploymentMessage = null, ByteCodeLibrary[] byteCodeLibraries = null, CancellationToken cancellationToken = default)
             where TDeploymentMessage : Nethereum.Contracts.ContractDeploymentMessage, new()
            {
                if (deploymentMessage == null)
                {
                    deploymentMessage = new TDeploymentMessage();
                }

                if (byteCodeLibraries != null)
                {
                    deploymentMessage.LinkLibraries(byteCodeLibraries);
                }

                return web3.Eth.Create2DeterministicDeploymentProxyService.DeployContractRequestAndWaitForReceiptAsync(deploymentMessage, deployerAddress, salt, byteCodeLibraries, cancellationToken);

            }

             public Nethereum.Web3.IWeb3 Web3 { get; }

            public ContractWeb3ServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress)
            {
                Web3 = web3;
                ContractHandler = web3.Eth.GetContractHandler(contractAddress);
            }

        }
}
